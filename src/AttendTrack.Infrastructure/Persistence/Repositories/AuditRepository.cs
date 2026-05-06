using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Infrastructure.Security;
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
        // Caller passes IST work dates; OccurredAt is stored as UTC.
        // Convert IST midnight → UTC so the range matches what's actually persisted.
        var fromUtc = IstTimeHelper.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var toUtc   = IstTimeHelper.ToUtc(to.ToDateTime(TimeOnly.MaxValue));

        return await _db.AuditLogs
            .Where(a => a.ActorId      == actorId
                     && a.OccurredAt  >= fromUtc
                     && a.OccurredAt  <= toUtc)
            .OrderByDescending(a => a.OccurredAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
