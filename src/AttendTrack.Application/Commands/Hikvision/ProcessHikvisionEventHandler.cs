using AttendTrack.Application.Common;
using AttendTrack.Application.Options;
using AttendTrack.Application.Parsers;
using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Exceptions;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Type aliases to prevent shadowing by sub-namespaces Commands.Employee / Commands.Shift
using DomainEmployee = AttendTrack.Domain.Entities.Employee;
using DomainShift    = AttendTrack.Domain.Entities.Shift;

namespace AttendTrack.Application.Commands.Hikvision;

/// <summary>
/// Handles inbound Hikvision ISAPI event push.
/// Archives event FIRST, then resolves employee, saves JPEG, routes to attendance action.
/// Always completes without throwing — bad events are logged and marked failed in HikvisionEventLog.
/// </summary>
public sealed class ProcessHikvisionEventHandler
    : IRequestHandler<ProcessHikvisionEventCommand, HikvisionProcessResult>
{
    private readonly IHikvisionDeviceRepository _deviceRepo;
    private readonly IEmployeeRepository        _employeeRepo;
    private readonly IAttendanceRepository      _attendanceRepo;
    private readonly IShiftRepository           _shiftRepo;
    private readonly IUnitOfWork                _uow;
    private readonly HikvisionOptions           _options;
    private readonly IAttendanceNotifier        _notifier;
    private readonly ILogger<ProcessHikvisionEventHandler> _logger;

    public ProcessHikvisionEventHandler(
        IHikvisionDeviceRepository             deviceRepo,
        IEmployeeRepository                    employeeRepo,
        IAttendanceRepository                  attendanceRepo,
        IShiftRepository                       shiftRepo,
        IUnitOfWork                            uow,
        IOptions<HikvisionOptions>             options,
        IAttendanceNotifier                    notifier,
        ILogger<ProcessHikvisionEventHandler>  logger)
    {
        _deviceRepo     = deviceRepo;
        _employeeRepo   = employeeRepo;
        _attendanceRepo = attendanceRepo;
        _shiftRepo      = shiftRepo;
        _uow            = uow;
        _options        = options.Value;
        _notifier       = notifier;
        _logger         = logger;
    }

    public async Task<HikvisionProcessResult> Handle(
        ProcessHikvisionEventCommand cmd, CancellationToken ct)
    {
        // ── Step 1: Parse XML — malformed payload is logged and absorbed ─────────
        HikvisionEventParsed parsed;
        try
        {
            parsed = HikvisionEventParser.Parse(cmd.RawXml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Malformed Hikvision XML payload — discarded");
            return new HikvisionProcessResult(
                EmployeeCode: "", AttendanceStatus: "", VerifyMode: "", AttendanceRecordId: null);
        }

        // Reject events with no employee code or no attendance status — they can't be processed
        if (string.IsNullOrWhiteSpace(parsed.EmployeeNoString)
            || string.IsNullOrWhiteSpace(parsed.AttendanceStatus))
        {
            _logger.LogWarning(
                "Hikvision event missing employeeNoString or attendanceStatus — discarded ({Serial})",
                parsed.DeviceSerial);
            return new HikvisionProcessResult(
                EmployeeCode: parsed.EmployeeNoString ?? "",
                AttendanceStatus: parsed.AttendanceStatus ?? "",
                VerifyMode: parsed.CurrentVerifyMode ?? "",
                AttendanceRecordId: null);
        }

        _logger.LogInformation(
            "Hikvision event received: {EmpCode} {Status} {Mode} from {Serial}",
            parsed.EmployeeNoString, parsed.AttendanceStatus,
            parsed.CurrentVerifyMode, parsed.DeviceSerial);

        // ── Step 2: Archive raw event FIRST (idempotency + DPDP audit trail) ────
        // Device-sourced fields have no length validation anywhere before this point —
        // only the target columns are capped (HikvisionEventLogConfiguration). A firmware
        // quirk or malformed/spoofed payload exceeding a cap used to throw Postgres 22001
        // ("value too long") from SaveChangesAsync below, uncaught by IsUniqueViolation
        // (which only matches 23505), producing a 500 instead of the documented
        // "always 200" contract — and since the device retries on any non-200, that meant
        // an endless retry of the same oversized payload. Truncate to the column caps
        // instead of validating-and-rejecting: this handler's whole design is to absorb
        // bad device data, not bounce it.
        var eventLog = HikvisionEventLog.Create(
            // Truncate's input is non-null for these five (HikvisionEventParsed's
            // corresponding properties are non-nullable string), so its output is
            // provably non-null too — the ! just tells the compiler what's already true.
            deviceSerialNumber: Truncate(parsed.DeviceSerial,      100)!,
            employeeCode:       Truncate(parsed.EmployeeNoString,   20)!,
            employeeName:       Truncate(parsed.EmployeeName,      200)!,
            attendanceStatus:   Truncate(parsed.AttendanceStatus,   30)!,
            verifyMode:         Truncate(parsed.CurrentVerifyMode,  50)!,
            cardNo:             Truncate(parsed.CardNo,            100),
            deviceLocalTime:    parsed.DeviceLocalTime,
            receivedAtUtc:      cmd.ReceivedAtUtc,
            rawPayload:         cmd.RawXml);

        try
        {
            await _deviceRepo.AddEventLogAsync(eventLog, ct);
            await _uow.SaveChangesAsync(ct);   // Persists even if subsequent steps fail
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            // Exact same event was already archived → idempotent return, device gets 200
            _logger.LogInformation(
                "Duplicate Hikvision event for {Code} at {Time} — already processed, skipping",
                parsed.EmployeeNoString, parsed.DeviceLocalTime);
            return new HikvisionProcessResult(
                EmployeeCode:       parsed.EmployeeNoString,
                AttendanceStatus:   parsed.AttendanceStatus,
                VerifyMode:         parsed.CurrentVerifyMode,
                AttendanceRecordId: null);
        }

        // ── Step 3: Update device last-seen timestamp ─────────────────────────────
        var device = await _deviceRepo.GetBySerialAsync(parsed.DeviceSerial, ct);
        if (device is not null)
            await _deviceRepo.UpdateLastEventAsync(device.Id, cmd.ReceivedAtUtc, ct);

        // ── Step 4: Resolve employee by employeeNoString ──────────────────────────
        var employee = await _employeeRepo.GetByCodeAsync(parsed.EmployeeNoString, ct);
        if (employee is null)
        {
            _logger.LogWarning(
                "Hikvision event for unrecognized employee code '{Code}'", parsed.EmployeeNoString);
            eventLog.MarkFailed($"Employee not found: {parsed.EmployeeNoString}");
            await _uow.SaveChangesAsync(ct);
            return HikvisionProcessResult.Unrecognized(parsed.EmployeeNoString);
        }

        // ── Step 5: Save JPEG face capture ──────────────────────────────────────
        string? facePath = null;
        if (cmd.FaceCaptureBytes is { Length: > 0 })
            facePath = await SaveFaceCaptureAsync(
                employee.EmployeeCode, parsed.DeviceLocalTime, cmd.FaceCaptureBytes, ct);

        eventLog.SetFaceCapturePath(facePath ?? string.Empty);

        // ── Step 6: Load shift ────────────────────────────────────────────────────
        var shift = await _shiftRepo.GetByIdAsync(employee.DefaultShiftId, ct);
        if (shift is null)
        {
            eventLog.MarkFailed($"Shift not found: {employee.DefaultShiftId}");
            await _uow.SaveChangesAsync(ct);
            return HikvisionProcessResult.Unrecognized(parsed.EmployeeNoString);
        }

        // ── Step 7: Route to attendance action ────────────────────────────────────
        AttendanceRecord? record = null;
        try
        {
            record = parsed.AttendanceStatus switch
            {
                "checkIn"  => await ProcessCheckInAsync(employee, shift, parsed, facePath, ct),
                "checkOut" => await ProcessCheckOutAsync(employee, shift, parsed, ct),
                "breakIn"  => await ProcessBreakStartAsync(employee, parsed, ct),
                "breakOut" => await ProcessBreakEndAsync(employee, parsed, ct),
                _ => null
            };

            if (record is not null)
            {
                eventLog.MarkProcessed(record.Id);
                _notifier.SignalAttendanceChanged();
            }

            // Must be inside this try: ProcessCheckInAsync/ProcessCheckOutAsync/etc. only
            // mutate the EF change tracker in memory (AddAsync/Update do no DB round trip)
            // — the actual UNIQUE(employee_id, work_date) constraint is only evaluated
            // here. Previously this call sat *outside* any try/catch, so the exact race
            // this handler claims to guard against (two near-simultaneous events for the
            // same employee+day both passing the in-memory duplicate check before either
            // commits) threw an unhandled DbUpdateException, producing a 500 instead of
            // the documented "always 200, device retries on non-200" contract — and since
            // the device retries on any non-200, that meant a permanent retry storm of the
            // same doomed event.
            await _uow.SaveChangesAsync(ct);
        }
        catch (AlreadyCheckedInException)
        {
            // Thrown synchronously inside ProcessCheckInAsync, before any DB write — safe
            // to mark the event failed and save again; nothing else is pending.
            _logger.LogInformation(
                "Duplicate check-in for {Code} — idempotent ignore", parsed.EmployeeNoString);
            eventLog.MarkFailed("Duplicate check-in — idempotent ignore");
            record = null;
            await _uow.SaveChangesAsync(ct);
        }
        catch (AlreadyCheckedOutException)
        {
            _logger.LogInformation(
                "Duplicate check-out for {Code} — idempotent ignore", parsed.EmployeeNoString);
            eventLog.MarkFailed("Duplicate check-out — idempotent ignore");
            record = null;
            await _uow.SaveChangesAsync(ct);
        }
        catch (ConcurrencyException ex)
        {
            // AttendanceRecord.Version (xmin) conflict. checkOut/breakIn/breakOut all
            // load-then-Update an existing tracked record — if another writer (the
            // webhook and polling fallback delivering the same event, or two
            // near-simultaneous device events) already bumped its xmin since this
            // handler loaded it, SaveChangesAsync throws DbUpdateConcurrencyException,
            // which UnitOfWork wraps into this ConcurrencyException. Previously only
            // the checkIn unique-violation race below was caught here — this one
            // propagated uncaught, producing the same 500-instead-of-200 failure this
            // handler otherwise absorbs, and since the device retries on any non-200,
            // a permanent retry storm of an event that will keep losing the race.
            // Not retrying SaveChangesAsync, same reasoning as the unique-violation
            // catch below: `record` is tracked in a failed Modified state, retrying
            // would just replay the same failing update.
            _logger.LogWarning(ex,
                "Concurrency conflict for {Code} — ignored", parsed.EmployeeNoString);
            record = null;
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            // Deliberately NOT retrying SaveChangesAsync here: `record` is still tracked
            // in a failed Added/Modified state from the switch above, and EF would just
            // replay the same failing insert/update. eventLog keeps its Step 2
            // "unprocessed" state rather than being marked failed — a minor loss of audit
            // detail, not a regression (today this path never even reaches here, it 500s).
            _logger.LogWarning(ex,
                "Concurrent race condition for {Code} — ignored", parsed.EmployeeNoString);
            record = null;
        }

        return new HikvisionProcessResult(
            EmployeeCode:       parsed.EmployeeNoString,
            AttendanceStatus:   parsed.AttendanceStatus,
            VerifyMode:         parsed.CurrentVerifyMode,
            AttendanceRecordId: record?.Id);
    }

    // ─── Private action helpers ────────────────────────────────────────────────

    private async Task<AttendanceRecord> ProcessCheckInAsync(
        DomainEmployee employee, DomainShift shift, HikvisionEventParsed parsed,
        string? facePath, CancellationToken ct)
    {
        var workDate = DateOnly.FromDateTime(parsed.DeviceLocalTime);
        var existing = await _attendanceRepo.GetByEmployeeAndDateAsync(employee.Id, workDate, ct);
        if (existing?.CheckInTime is not null)
            throw new AlreadyCheckedInException(employee.Id.Value);

        var record = AttendanceRecord.CheckIn(
            employeeId:           employee.Id,
            shift:                shift,
            workDate:             workDate,
            checkInTimeUtc:       parsed.EventTimeUtc,
            source:               PunchSource.Hikvision,
            verifyMode:           parsed.CurrentVerifyMode,
            deviceSerial:         parsed.DeviceSerial,
            faceCaptureImagePath: facePath);

        await _attendanceRepo.AddAsync(record, ct);
        return record;
    }

    private async Task<AttendanceRecord?> ProcessCheckOutAsync(
        DomainEmployee employee, DomainShift shift, HikvisionEventParsed parsed, CancellationToken ct)
    {
        var workDate = DateOnly.FromDateTime(parsed.DeviceLocalTime);
        var record   = await _attendanceRepo.GetByEmployeeAndDateAsync(employee.Id, workDate, ct);
        if (record is null)
        {
            _logger.LogWarning("CheckOut event for {Code} with no open check-in on {Date}",
                employee.EmployeeCode, workDate);
            return null;
        }

        record.CheckOut(shift, parsed.EventTimeUtc, PunchSource.Hikvision, parsed.CurrentVerifyMode);
        _attendanceRepo.Update(record);
        return record;
    }

    private async Task<AttendanceRecord?> ProcessBreakStartAsync(
        DomainEmployee employee, HikvisionEventParsed parsed, CancellationToken ct)
    {
        var workDate = DateOnly.FromDateTime(parsed.DeviceLocalTime);
        var record   = await _attendanceRepo.GetByEmployeeAndDateAsync(employee.Id, workDate, ct);
        if (record is null) return null;

        record.StartBreak(BreakType.Short, PunchSource.Hikvision, parsed.EventTimeUtc);
        _attendanceRepo.Update(record);
        return record;
    }

    private async Task<AttendanceRecord?> ProcessBreakEndAsync(
        DomainEmployee employee, HikvisionEventParsed parsed, CancellationToken ct)
    {
        var workDate    = DateOnly.FromDateTime(parsed.DeviceLocalTime);
        var record      = await _attendanceRepo.GetByEmployeeAndDateAsync(employee.Id, workDate, ct);
        if (record is null) return null;

        var activeBreak = record.Breaks.FirstOrDefault(b => b.IsActive);
        if (activeBreak is null)
        {
            _logger.LogWarning("BreakOut for {Code} with no active break", employee.EmployeeCode);
            return record;
        }

        record.EndBreak(activeBreak.Id, PunchSource.Hikvision, parsed.EventTimeUtc);
        _attendanceRepo.Update(record);
        return record;
    }

    private async Task<string> SaveFaceCaptureAsync(
        string employeeCode, DateTime istTime, byte[] jpegBytes, CancellationToken ct)
    {
        var dir      = Path.Combine(_options.FaceCaptureBasePath,
                           istTime.ToString("yyyyMMdd"));
        var filename = $"{employeeCode}_{istTime:HHmmss}.jpg";
        var fullPath = Path.Combine(dir, filename);

        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(fullPath, jpegBytes, ct);

        _logger.LogDebug("Face capture saved: {Path}", fullPath);
        return fullPath;
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    private static bool IsUniqueViolation(Exception ex)
    {
        var inner = ex.InnerException;
        while (inner is not null)
        {
            if (inner.Message.Contains("23505", StringComparison.Ordinal)          // PostgreSQL
             || inner.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase))
                return true;
            inner = inner.InnerException;
        }
        return ex.Message.Contains("23505", StringComparison.Ordinal)
            || ex.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
    }
}
