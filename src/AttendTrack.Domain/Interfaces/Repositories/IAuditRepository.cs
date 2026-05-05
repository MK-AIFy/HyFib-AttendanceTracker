using AttendTrack.Domain.Entities;

namespace AttendTrack.Domain.Interfaces.Repositories;

public interface IAuditRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByActorAsync(Guid actorId, DateOnly from, DateOnly to, CancellationToken ct = default);
}
