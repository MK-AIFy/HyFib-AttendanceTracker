namespace AttendTrack.Domain.ValueObjects;

/// <summary>
/// Parsed representation of a Hikvision DS-K1T320MFWX ISAPI event.
/// Used as return type from IHikvisionIsapiService polling.
/// </summary>
public sealed record HikvisionEventParsed(
    string DeviceSerial,
    string EmployeeNoString,
    string EmployeeName,
    string AttendanceStatus,    // checkIn / checkOut / breakIn / breakOut
    string CurrentVerifyMode,   // faceAndFp / face / fp / card / pin
    string? CardNo,
    DateTime EventTimeUtc,
    DateTime DeviceLocalTime);  // IST — parsed from device dateTime field
