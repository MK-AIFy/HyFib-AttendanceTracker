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

    public async Task AddAsync(HourlySlot slot, CancellationToken ct = default)
        => await _db.HourlySlots.AddAsync(slot, ct).ConfigureAwait(false);

    public async Task AddRangeAsync(IEnumerable<HourlySlot> slots, CancellationToken ct = default)
        => await _db.HourlySlots.AddRangeAsync(slots, ct).ConfigureAwait(false);

    public void Update(HourlySlot slot)
        => _db.HourlySlots.Update(slot);

    /// <summary>
    /// Insert or update a slot atomically.
    /// Called by HourlyTrackerService every 60 s (Gap 4).
    /// </summary>
    public async Task UpsertAsync(HourlySlot slot, CancellationToken ct = default)
    {
        var existing = await GetAsync(slot.AttendanceRecordId, slot.HourSlotNumber, ct)
            .ConfigureAwait(false);

        if (existing is null)
            await _db.HourlySlots.AddAsync(slot, ct).ConfigureAwait(false);
        else
        {
            existing.Update(slot.MinutesWorked, slot.IsBreak, slot.IsOvertime);
            _db.HourlySlots.Update(existing);
        }
    }
}

