using AttendTrack.Domain.Common;

namespace AttendTrack.Domain.Events;

public sealed record OvertimeThresholdReachedEvent(
    Guid EmployeeId,
    Guid AttendanceRecordId,
    DateOnly WorkDate,
    TimeSpan OvertimeDuration,
    DateTime OccurredAtUtc) : IDomainEvent;
