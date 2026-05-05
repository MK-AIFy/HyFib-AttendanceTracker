namespace AttendTrack.Domain.ValueObjects;

/// <summary>Strongly-typed identifier for Employee aggregate root.</summary>
public sealed record EmployeeId(Guid Value)
{
    public static EmployeeId New()         => new(Guid.NewGuid());
    public static EmployeeId From(Guid value) => new(value);
    public override string ToString()     => Value.ToString();
}
