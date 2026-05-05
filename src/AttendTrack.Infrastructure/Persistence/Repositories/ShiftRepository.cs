using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AttendTrack.Infrastructure.Persistence.Repositories;

public sealed class ShiftRepository : IShiftRepository
{
    private readonly AttendTrackDbContext _db;
    public ShiftRepository(AttendTrackDbContext db) => _db = db;

    public async Task<Shift?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Shifts.FindAsync([id], ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<Shift>> GetAllActiveAsync(CancellationToken ct = default)
        => await _db.Shifts
            .Where(s => s.IsActive)
            .OrderBy(s => s.StartTime)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task AddAsync(Shift shift, CancellationToken ct = default)
        => await _db.Shifts.AddAsync(shift, ct).ConfigureAwait(false);

    public void Update(Shift shift)
        => _db.Shifts.Update(shift);
}

