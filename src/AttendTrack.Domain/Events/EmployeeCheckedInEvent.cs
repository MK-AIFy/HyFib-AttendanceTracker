using AttendTrack.Domain.Common;
using AttendTrack.Domain.Enums;

namespace AttendTrack.Domain.Events;

public sealed record EmployeeCheckedInEvent(
    Guid EmployeeId,
    Guid AttendanceRecordId,
    DateTime CheckInTimeUtc,
    PunchSource Source,
    string? VerifyMode) : IDomainEvent;
