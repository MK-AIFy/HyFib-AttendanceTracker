namespace AttendTrack.Domain.Exceptions;

public sealed class AlreadyCheckedOutException : DomainException
{
    public Guid EmployeeId { get; }

    public AlreadyCheckedOutException(Guid employeeId)
        : base($"Employee {employeeId} has already checked out today.")
        => EmployeeId = employeeId;

    public AlreadyCheckedOutException(Guid employeeId, string message)
        : base(message)
        => EmployeeId = employeeId;
}
