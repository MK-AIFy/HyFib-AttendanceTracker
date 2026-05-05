namespace AttendTrack.Domain.Common;

/// <summary>
/// Non-generic interface implemented by all AggregateRoot&lt;TId&gt; instances.
/// Used by AttendTrackDbContext.SaveChangesAsync to collect and dispatch domain events
/// without needing to know the generic TId type parameter.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
