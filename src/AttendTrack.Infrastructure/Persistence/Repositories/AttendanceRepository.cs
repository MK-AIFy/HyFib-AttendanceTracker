using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AttendTrack.Infrastructure.Persistence.Repositories;

public sealed class AttendanceRepository : IAttendanceRepository
{
    private readonly AttendTrackDbContext _db;
    public AttendanceRepository(AttendTrackDbContext db) => _db = db;

    public async Task<AttendanceRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.AttendanceRecords
            .Include(r => r.Breaks)
            .Include(r => r.HourlySlots)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            .ConfigureAwait(false);

    public async Task<AttendanceRecord?> GetByEmployeeAndDateAsync(
        EmployeeId employeeId, DateOnly workDate, CancellationToken ct = default)
        => await _db.AttendanceRecords
            .Include(r => r.Breaks)
            .Include(r => r.HourlySlots)
            .FirstOrDefaultAsync(
                r => r.EmployeeId == employeeId && r.WorkDate == workDate, ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AttendanceRecord>> GetByDateAsync(
        DateOnly date, CancellationToken ct = default)
        => await _db.AttendanceRecords
            .Include(r => r.Breaks)
            .Where(r => r.WorkDate == date)
            .OrderBy(r => r.CheckInTime)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AttendanceRecord>> GetByEmployeeAsync(
        EmployeeId employeeId, DateOnly from, DateOnly to, CancellationToken ct = default)
        => await _db.AttendanceRecords
            .Include(r => r.Breaks)
            .Include(r => r.HourlySlots)
            .Where(r => r.EmployeeId == employeeId
                     && r.WorkDate   >= from
                     && r.WorkDate   <= to)
            .OrderBy(r => r.WorkDate)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    /// <summary>
    /// Returns open check-ins (CheckOutTime is null) where check-in was recorded
    /// before the given UTC threshold. Used by MissedPunchDetectorService (Gap 1).
    /// </summary>
    public async Task<IReadOnlyList<AttendanceRecord>> GetOpenCheckInsOlderThanAsync(
        DateTime thresholdUtc, CancellationToken ct = default)
        => await _db.AttendanceRecords
            .Where(r => r.CheckOutTime == null
                     && r.CheckInTime  != null
                     && r.CheckInTime  < thresholdUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AttendanceRecord>> GetByDateRangeAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
        => await _db.AttendanceRecords
            .Include(r => r.Breaks)
            .Where(r => r.WorkDate >= from && r.WorkDate <= to)
            .OrderBy(r => r.WorkDate)
            .ThenBy(r => r.CheckInTime)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task AddAsync(AttendanceRecord record, CancellationToken ct = default)
        => await _db.AttendanceRecords.AddAsync(record, ct).ConfigureAwait(false);

    public void Update(AttendanceRecord record)
        => _db.AttendanceRecords.Update(record);
}

