using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AttendTrack.Infrastructure.Persistence.Repositories;

public sealed class HourlySlotRepository : IHourlySlotRepository
{
    private readonly AttendTrackDbContext _db;
    public HourlySlotRepository(AttendTrackDbContext db) => _db = db;

    public async Task<HourlySlot?> GetAsync(
        Guid attendanceRecordId, int hourSlot, CancellationToken ct = default)
        => await _db.HourlySlots
            .FirstOrDefaultAsync(
                s => s.AttendanceRecordId == attendanceRecordId
                  && s.HourSlotNumber     == hourSlot, ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<HourlySlot>> GetByAttendanceAsync(
        Guid attendanceRecordId, CancellationToken ct = default)
        => await _db.HourlySlots
            .Where(s => s.AttendanceRecordId == attendanceRecordId)
            .OrderBy(s => s.HourSlotNumber)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<HourlySlot>> GetByDateAsync(
        DateOnly workDate, CancellationToken ct = default)
        => await _db.HourlySlots
            .Where(s => s.WorkDate == workDate)
            .OrderBy(s => s.EmployeeId)
            .ThenBy(s => s.HourSlotNumber)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<HourlySlot>>> GetByAttendanceIdsAsync(
        IEnumerable<Guid> attendanceRecordIds, CancellationToken ct = default)
    {
        var ids = attendanceRecordIds as ICollection<Guid> ?? attendanceRecordIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<HourlySlot>>();

        var slots = await _db.HourlySlots
            .Where(s => ids.Contains(s.AttendanceRecordId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return slots
            .GroupBy(s => s.AttendanceRecordId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<HourlySlot>)g.ToList());
    }

    public async Task AddAsync(HourlySlot slot, CancellationToken ct = default)
        => await _db.HourlySlots.AddAsync(slot, ct).ConfigureAwait(false);

    public async Task AddRangeAsync(IEnumerable<HourlySlot> slots, CancellationToken ct = default)
        => await _db.HourlySlots.AddRangeAsync(slots, ct).ConfigureAwait(false);

    public void Update(HourlySlot slot)
        => _db.HourlySlots.Update(slot);
}

