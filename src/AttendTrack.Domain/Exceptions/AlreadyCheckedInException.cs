namespace AttendTrack.Domain.Exceptions;

public sealed class AlreadyCheckedInException : DomainException
{
    public Guid EmployeeId { get; }

    public AlreadyCheckedInException(Guid employeeId)
        : base($"Employee {employeeId} is already checked in today.")
        => EmployeeId = employeeId;

    public AlreadyCheckedInException(Guid employeeId, string message)
        : base(message)
        => EmployeeId = employeeId;
}
