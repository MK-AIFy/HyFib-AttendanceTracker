using AttendTrack.Domain.Common;
using AttendTrack.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AttendTrack.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for AttendTrack Enterprise.
/// <list type="bullet">
///   <item>All entity configurations are applied from assembly via ApplyConfigurationsFromAssembly.</item>
///   <item>SaveChangesAsync dispatches domain events after every successful save (event sourcing pattern).</item>
///   <item>JSONB columns (AuditLog.OldValues / NewValues) configured in AuditLogConfiguration.</item>
/// </list>
/// </summary>
public sealed class AttendTrackDbContext : DbContext
{
    private readonly IPublisher _publisher;

    public AttendTrackDbContext(
        DbContextOptions<AttendTrackDbContext> options,
        IPublisher                             publisher)
        : base(options)
    {
        _publisher = publisher;
    }

    // ── Core attendance entities ───────────────────────────────────────────────
    public DbSet<Employee>         Employees         => Set<Employee>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<BreakRecord>      BreakRecords      => Set<BreakRecord>();
    public DbSet<HourlySlot>       HourlySlots       => Set<HourlySlot>();
    public DbSet<Shift>            Shifts            => Set<Shift>();
    public DbSet<Department>       Departments       => Set<Department>();
    public DbSet<AuditLog>         AuditLogs         => Set<AuditLog>();

    // ── Hikvision integration entities ────────────────────────────────────────
    public DbSet<HikvisionDevice>   HikvisionDevices   => Set<HikvisionDevice>();
    public DbSet<HikvisionEventLog> HikvisionEventLogs => Set<HikvisionEventLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AttendTrackDbContext).Assembly);
    }

    /// <summary>
    /// Saves changes then dispatches all accumulated domain events.
    /// Events are collected before the save so that even if a handler throws,
    /// the raw DB write is not lost.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Snapshot aggregates that hold pending events BEFORE the save
        var aggregatesWithEvents = ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Dispatch after successful save — events may trigger further DB writes.
        // IDomainEvent is a plain marker interface (Domain has zero dependencies, so it
        // can't implement MediatR's INotification directly) — wrap each event so
        // IPublisher.Publish recognizes it, instead of passing the raw IDomainEvent
        // object, which MediatR rejects with "notification does not implement INotification".
        foreach (var aggregate in aggregatesWithEvents)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
                await _publisher.Publish(WrapAsNotification(domainEvent), cancellationToken)
                    .ConfigureAwait(false);

            aggregate.ClearDomainEvents();
        }

        return result;
    }

    private static INotification WrapAsNotification(IDomainEvent domainEvent)
    {
        var wrapperType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
        return (INotification)Activator.CreateInstance(wrapperType, domainEvent)!;
    }
}

/// <summary>
/// Adapts a Domain-layer <see cref="IDomainEvent"/> (a dependency-free marker interface) into
/// a MediatR <see cref="INotification"/> so it can be dispatched via IPublisher.
/// Future handlers subscribe as INotificationHandler&lt;DomainEventNotification&lt;TEvent&gt;&gt;.
/// </summary>
public sealed class DomainEventNotification<TDomainEvent>(TDomainEvent domainEvent) : INotification
    where TDomainEvent : IDomainEvent
{
    public TDomainEvent DomainEvent { get; } = domainEvent;
}

