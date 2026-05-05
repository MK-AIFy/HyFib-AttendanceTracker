using AttendTrack.Domain.Common;

namespace AttendTrack.Domain.Events;

/// <summary>
/// Raised when a raw ISAPI event is received from a Hikvision device
/// and archived to HikvisionEventLog.
/// </summary>
public sealed record HikvisionEventReceivedEvent(
    Guid EventLogId,
    string DeviceSerialNumber,
    string EmployeeCode,
    string AttendanceStatus,
    DateTime DeviceLocalTime) : IDomainEvent;
