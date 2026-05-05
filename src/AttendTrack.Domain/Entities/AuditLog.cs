using AttendTrack.Domain.Common;

namespace AttendTrack.Domain.Entities;

/// <summary>
/// Immutable audit record. Supports DPDP Act 2023 compliance (Gap 6).
/// old_values / new_values stored as JSONB strings serialised by AuditBehaviour.
/// </summary>
public sealed class AuditLog : AggregateRoot<Guid>
{
    public Guid?    ActorId     { get; private set; }
    public string?  ActorRole   { get; private set; }
    public string   Action      { get; private set; } = default!;
    public string?  EntityType  { get; private set; }
    public Guid?    EntityId    { get; private set; }
    /// <summary>JSONB — state before the mutation.</summary>
    public string?  OldValues   { get; private set; }
    /// <summary>JSONB — state after the mutation.</summary>
    public string?  NewValues   { get; private set; }
    public string?  IpAddress   { get; private set; }
    public DateTime OccurredAt  { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        string  action,
        Guid?   actorId    = null,
        string? actorRole  = null,
        string? entityType = null,
        Guid?   entityId   = null,
        string? oldValues  = null,
        string? newValues  = null,
        string? ipAddress  = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        return new AuditLog
        {
            Id         = Guid.NewGuid(),
            Action     = action,
            ActorId    = actorId,
            ActorRole  = actorRole,
            EntityType = entityType,
            EntityId   = entityId,
            OldValues  = oldValues,
            NewValues  = newValues,
            IpAddress  = ipAddress,
            OccurredAt = DateTime.UtcNow
        };
    }
}
