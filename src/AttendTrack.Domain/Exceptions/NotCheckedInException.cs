namespace AttendTrack.Domain.Exceptions;

public sealed class NotCheckedInException : DomainException
{
    public Guid EmployeeId { get; }

    public NotCheckedInException(Guid employeeId)
        : base($"Employee {employeeId} has not checked in — cannot perform checkout or break action.")
        => EmployeeId = employeeId;

    public NotCheckedInException(Guid employeeId, string message)
        : base(message)
        => EmployeeId = employeeId;
}
