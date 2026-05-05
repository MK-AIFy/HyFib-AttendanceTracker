namespace AttendTrack.Domain.Common;

/// <summary>
/// Base class for all aggregate roots.
/// Holds a collection of domain events that are dispatched after persistence.
/// Implements <see cref="IHasDomainEvents"/> so EF Core context can dispatch events
/// without knowledge of the generic TId parameter.
/// </summary>
public abstract class AggregateRoot<TId> : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public TId Id { get; protected set; } = default!;
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
