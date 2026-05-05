using AttendTrack.Domain.Common;

namespace AttendTrack.Domain.Entities;

/// <summary>
/// Immutable archive of every raw ISAPI event received from a Hikvision device.
/// UNIQUE KEY: (DeviceSerialNumber, DeviceLocalTime, EmployeeCode) ensures idempotency.
/// Stores full XML payload for replay and DPDP audit trail.
/// </summary>
public sealed class HikvisionEventLog : AggregateRoot<Guid>
{
    // UNIQUE KEY parts
    public string    DeviceSerialNumber      { get; private set; } = default!;
    public string    EmployeeCode            { get; private set; } = default!;
    public DateTime  DeviceLocalTime         { get; private set; }  // IST, parsed from device dateTime

    public string    EmployeeName            { get; private set; } = default!;
    /// <summary>checkIn / checkOut / breakIn / breakOut</summary>
    public string    AttendanceStatus        { get; private set; } = default!;
    /// <summary>faceAndFp / face / fp / card / pin</summary>
    public string    VerifyMode              { get; private set; } = default!;
    public string?   CardNo                  { get; private set; }
    public DateTime  ReceivedAtUtc           { get; private set; }
    public string?   FaceCaptureStoragePath  { get; private set; }
    /// <summary>Full XML payload stored for replay/audit (DPDP Art. 7).</summary>
    public string    RawPayload              { get; private set; } = default!;
    public bool      IsProcessed             { get; private set; }
    public string?   ProcessingError         { get; private set; }
    /// <summary>Links to the AttendanceRecord created from this event.</summary>
    public Guid?     AttendanceRecordId      { get; private set; }

    private HikvisionEventLog() { }

    public static HikvisionEventLog Create(
        string   deviceSerialNumber,
        string   employeeCode,
        string   employeeName,
        string   attendanceStatus,
        string   verifyMode,
        string?  cardNo,
        DateTime deviceLocalTime,
        DateTime receivedAtUtc,
        string   rawPayload,
        string?  faceCaptureStoragePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPayload);

        return new HikvisionEventLog
        {
            Id                      = Guid.NewGuid(),
            DeviceSerialNumber      = deviceSerialNumber,
            EmployeeCode            = employeeCode,
            EmployeeName            = employeeName,
            AttendanceStatus        = attendanceStatus,
            VerifyMode              = verifyMode,
            CardNo                  = cardNo,
            DeviceLocalTime         = deviceLocalTime,
            ReceivedAtUtc           = receivedAtUtc,
            RawPayload              = rawPayload,
            FaceCaptureStoragePath  = faceCaptureStoragePath,
            IsProcessed             = false
        };
    }

    public void MarkProcessed(Guid attendanceRecordId)
    {
        IsProcessed       = true;
        AttendanceRecordId = attendanceRecordId;
        ProcessingError   = null;
    }

    public void MarkFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        IsProcessed     = false;
        ProcessingError = error;
    }

    public void SetFaceCapturePath(string path) => FaceCaptureStoragePath = path;
}
