using AttendTrack.Domain.Common;

namespace AttendTrack.Domain.Events;

/// <summary>
/// Raised when MissedPunchDetectorService auto-checks-out an employee
/// who forgot to check out (Gap 1).
/// </summary>
public sealed record MissedPunchDetectedEvent(
    Guid EmployeeId,
    DateOnly WorkDate,
    DateTime AutoCheckOutTimeUtc) : IDomainEvent;
