using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AttendTrack.Infrastructure.Persistence.Repositories;

public sealed class AuditRepository : IAuditRepository
{
    private readonly AttendTrackDbContext _db;
    public AuditRepository(AttendTrackDbContext db) => _db = db;

    public async Task AddAsync(AuditLog auditLog, CancellationToken ct = default)
        => await _db.AuditLogs.AddAsync(auditLog, ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<AuditLog>> GetByEntityAsync(
        string entityType, Guid entityId, CancellationToken ct = default)
        => await _db.AuditLogs
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.OccurredAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AuditLog>> GetByActorAsync(
        Guid actorId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc   = to.ToDateTime(TimeOnly.MaxValue,   DateTimeKind.Utc);

        return await _db.AuditLogs
            .Where(a => a.ActorId      == actorId
                     && a.OccurredAt  >= fromUtc
                     && a.OccurredAt  <= toUtc)
            .OrderByDescending(a => a.OccurredAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
