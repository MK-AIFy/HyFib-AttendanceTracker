namespace AttendTrack.Domain.Exceptions;

/// <summary>
/// Thrown when a concurrent write produces a database unique-violation
/// on (employee_id, work_date) — Gap 2 concurrency protection.
/// </summary>
public sealed class ConcurrentCheckInException : DomainException
{
    public Guid EmployeeId { get; }

    public ConcurrentCheckInException(Guid employeeId)
        : base($"Concurrent check-in detected for employee {employeeId}. Only one check-in per day is allowed.")
        => EmployeeId = employeeId;

    public ConcurrentCheckInException(Guid employeeId, string message)
        : base(message)
        => EmployeeId = employeeId;
}
