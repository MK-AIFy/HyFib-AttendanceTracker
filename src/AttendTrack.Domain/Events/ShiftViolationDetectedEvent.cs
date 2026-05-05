using AttendTrack.Domain.Common;
using AttendTrack.Domain.Enums;

namespace AttendTrack.Domain.Events;

/// <summary>
/// Raised when a late arrival or early departure is detected (Gap 8).
/// </summary>
public sealed record ShiftViolationDetectedEvent(
    Guid EmployeeId,
    Guid ShiftId,
    ShiftViolationType ViolationType,
    DateTime EventTimeUtc,
    string Detail) : IDomainEvent;
