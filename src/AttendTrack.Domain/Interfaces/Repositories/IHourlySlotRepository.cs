using AttendTrack.Domain.Entities;

namespace AttendTrack.Domain.Interfaces.Repositories;

public interface IHourlySlotRepository
{
    Task<HourlySlot?> GetAsync(Guid attendanceRecordId, int hourSlot, CancellationToken ct = default);
    Task<IReadOnlyList<HourlySlot>> GetByAttendanceAsync(Guid attendanceRecordId, CancellationToken ct = default);
    Task<IReadOnlyList<HourlySlot>> GetByDateAsync(DateOnly workDate, CancellationToken ct = default);

    /// <summary>
    /// Batch-loads every existing HourlySlot row for the given AttendanceRecordIds
    /// in a single query, grouped by AttendanceRecordId. Used by HourlyTrackerService
    /// to avoid a SELECT-then-write round trip per slot per open record per tick.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<HourlySlot>>> GetByAttendanceIdsAsync(
        IEnumerable<Guid> attendanceRecordIds, CancellationToken ct = default);

    Task AddAsync(HourlySlot slot, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<HourlySlot> slots, CancellationToken ct = default);
    void Update(HourlySlot slot);
}
