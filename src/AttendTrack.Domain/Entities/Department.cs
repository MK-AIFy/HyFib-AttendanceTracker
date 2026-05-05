using AttendTrack.Domain.Common;

namespace AttendTrack.Domain.Entities;

public sealed class Department : AggregateRoot<Guid>
{
    public string Name        { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool   IsActive    { get; private set; }
    public DateTime CreatedAt  { get; private set; }

    private Department() { }

    public static Department Create(Guid id, string name, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Department
        {
            Id          = id,
            Name        = name,
            Description = description,
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public void Deactivate() => IsActive = false;
}
