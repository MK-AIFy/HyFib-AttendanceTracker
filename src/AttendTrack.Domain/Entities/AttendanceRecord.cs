using AttendTrack.Domain.Common;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Events;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.ValueObjects;

namespace AttendTrack.Domain.Entities;

/// <summary>
/// Core aggregate root representing one employee's attendance for a single IST work date.
/// PRIMARY source of truth is Hikvision ISAPI event push (PunchSource.Hikvision).
/// Blazor PIN kiosk is the fallback source (PunchSource.BlazorKiosk).
///
/// Concurrency: protected by UNIQUE(employee_id, work_date) + PostgreSQL xmin token (Gap 2).
/// </summary>
public sealed class AttendanceRecord : AggregateRoot<Guid>
{
    // IST timezone — shared static field; fallback for Windows dev machines
    private static readonly TimeZoneInfo IstZone = GetIstZone();

    private readonly List<BreakRecord> _breaks      = [];
    private readonly List<HourlySlot>  _hourlySlots = [];

    public EmployeeId       EmployeeId            { get; private set; } = default!;
    /// <summary>Work date in IST — derived from CheckInTime converted to IST, NOT UTC date.</summary>
    public DateOnly         WorkDate              { get; private set; }
    public DateTime?        CheckInTime           { get; private set; }   // UTC TIMESTAMPTZ
    public DateTime?        CheckOutTime          { get; private set; }   // UTC TIMESTAMPTZ
    public AttendanceStatus Status                { get; private set; }
    /// <summary>PRIMARY punch source for this record (Hikvision / BlazorKiosk / Manual).</summary>
    public PunchSource      CheckInSource         { get; private set; }
    public PunchSource?     CheckOutSource        { get; private set; }
    /// <summary>Hikvision currentVerifyMode: faceAndFp / face / fp / card / pin.</summary>
    public string?          VerifyMode            { get; private set; }
    /// <summary>Path to JPEG face capture sent by the device with the event.</summary>
    public string?          FaceCaptureImagePath  { get; private set; }
    /// <summary>Device serial number from the ISAPI event payload (audit trail).</summary>
    public string?          HikvisionSerialNo     { get; private set; }
    public string?          DeviceId              { get; private set; }
    /// <summary>Violation notes (late arrival, early departure, auto-checkout reason).</summary>
    public string?          Notes                 { get; private set; }
    /// <summary>PostgreSQL xmin concurrency token — mapped via UseXminAsConcurrencyToken (Gap 2).</summary>
    public uint             Version               { get; private set; }
    public TimeSpan         BreakDuration         { get; private set; }
    public DateTime         CreatedAt             { get; private set; }

    public IReadOnlyCollection<BreakRecord> Breaks      => _breaks.AsReadOnly();
    public IReadOnlyCollection<HourlySlot>  HourlySlots => _hourlySlots.AsReadOnly();

    private AttendanceRecord() { }

    /// <summary>
    /// Factory: creates attendance record for a check-in event.
    /// Detects late arrival against shift start + grace period and raises
    /// <see cref="ShiftViolationDetectedEvent"/> if applicable.
    /// </summary>
    public static AttendanceRecord CheckIn(
        EmployeeId  employeeId,
        Shift       shift,
        DateOnly    workDate,
        DateTime    checkInTimeUtc,
        PunchSource source,
        string?     verifyMode           = null,
        string?     deviceSerial         = null,
        string?     faceCaptureImagePath = null)
    {
        var record = new AttendanceRecord
        {
            Id                   = Guid.NewGuid(),
            EmployeeId           = employeeId,
            WorkDate             = workDate,
            CheckInTime          = checkInTimeUtc,
            Status               = AttendanceStatus.Present,
            CheckInSource        = source,
            VerifyMode           = verifyMode,
            HikvisionSerialNo    = deviceSerial,
            FaceCaptureImagePath = faceCaptureImagePath,
            CreatedAt            = DateTime.UtcNow
        };

        // --- Shift violation: late arrival ---
        var checkInIst      = TimeZoneInfo.ConvertTimeFromUtc(checkInTimeUtc, IstZone);
        var checkInTimeOnly = TimeOnly.FromDateTime(checkInIst);
        var lateThreshold   = shift.StartTime.AddMinutes(shift.GracePeriodMinutes);

        if (checkInTimeOnly > lateThreshold)
        {
            var minutesLate = (int)(checkInTimeOnly - shift.StartTime).TotalMinutes;
            record.Notes = $"Late arrival: {checkInIst:HH:mm} IST (shift starts {shift.StartTime:HH:mm}, late by {minutesLate} min)";
            record.AddDomainEvent(new ShiftViolationDetectedEvent(
                employeeId.Value, shift.Id, ShiftViolationType.LateArrival,
                checkInTimeUtc, record.Notes));
        }

        record.AddDomainEvent(new EmployeeCheckedInEvent(
            employeeId.Value, record.Id, checkInTimeUtc, source, verifyMode));

        return record;
    }

    /// <summary>
    /// Records check-out for an existing attendance record.
    /// Detects early departure and raises <see cref="ShiftViolationDetectedEvent"/>.
    /// </summary>
    public void CheckOut(
        Shift       shift,
        DateTime    checkOutTimeUtc,
        PunchSource source,
        string?     verifyMode = null)
    {
        if (CheckInTime is null)
            throw new NotCheckedInException(EmployeeId.Value);
        if (CheckOutTime is not null)
            throw new AlreadyCheckedOutException(EmployeeId.Value);

        CheckOutTime   = checkOutTimeUtc;
        CheckOutSource = source;

        // --- Shift violation: early departure ---
        var checkOutIst      = TimeZoneInfo.ConvertTimeFromUtc(checkOutTimeUtc, IstZone);
        var checkOutTimeOnly = TimeOnly.FromDateTime(checkOutIst);

        if (checkOutTimeOnly < shift.EndTime)
        {
            var minutesEarly = (int)(shift.EndTime - checkOutTimeOnly).TotalMinutes;
            var note = $"Early departure: {checkOutIst:HH:mm} IST (shift ends {shift.EndTime:HH:mm}, early by {minutesEarly} min)";
            Notes = Notes is null ? note : Notes + "; " + note;
            AddDomainEvent(new ShiftViolationDetectedEvent(
                EmployeeId.Value, shift.Id, ShiftViolationType.EarlyDeparture,
                checkOutTimeUtc, note));
        }

        AddDomainEvent(new EmployeeCheckedOutEvent(EmployeeId.Value, Id, checkOutTimeUtc, source));
    }

    /// <summary>
    /// Called by <c>MissedPunchDetectorService</c> when an open check-in is detected
    /// beyond the auto-checkout threshold (Gap 1).
    /// Sets Status = MissedCheckout and raises <see cref="MissedPunchDetectedEvent"/>.
    /// </summary>
    public void ForceAutoCheckOut(DateTime autoTimeUtc)
    {
        CheckOutTime   = autoTimeUtc;
        CheckOutSource = PunchSource.Manual;
        Status         = AttendanceStatus.MissedCheckout;

        var note = $"Auto-checked out by system at {autoTimeUtc:yyyy-MM-dd HH:mm} UTC";
        Notes = Notes is null ? note : Notes + "; " + note;

        AddDomainEvent(new MissedPunchDetectedEvent(EmployeeId.Value, WorkDate, autoTimeUtc));
    }

    /// <summary>
    /// Overrides the check-in timestamp with the authoritative device event time.
    /// Used when the server-received time differs from the Hikvision device's reported time.
    /// </summary>
    public void OverrideCheckInTime(DateTime deviceEventTimeUtc)
        => CheckInTime = deviceEventTimeUtc;

    public BreakRecord StartBreak(BreakType type, PunchSource source, DateTime startTimeUtc)
    {
        var br = BreakRecord.Create(Id, EmployeeId.Value, startTimeUtc, type, source);
        _breaks.Add(br);
        return br;
    }

    public void EndBreak(Guid breakRecordId, PunchSource source, DateTime endTimeUtc)
    {
        var br = _breaks.FirstOrDefault(b => b.Id == breakRecordId)
            ?? throw new DomainException($"Break {breakRecordId} not found on attendance record {Id}.");
        br.End(source, endTimeUtc);
        RecalculateBreakDuration();
    }

    private void RecalculateBreakDuration()
        => BreakDuration = _breaks.Aggregate(TimeSpan.Zero, (acc, b) => acc + b.Duration);

    private static TimeZoneInfo GetIstZone()
    {
        // IANA ID on macOS / Linux Docker; Windows uses a different ID
        if (TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Kolkata", out var tz)) return tz;
        return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
    }
}
