using AttendTrack.Domain.Common;
using AttendTrack.Domain.Enums;

namespace AttendTrack.Domain.Events;

public sealed record EmployeeCheckedOutEvent(
    Guid EmployeeId,
    Guid AttendanceRecordId,
    DateTime CheckOutTimeUtc,
    PunchSource Source) : IDomainEvent;
