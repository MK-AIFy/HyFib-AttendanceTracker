using AttendTrack.Domain.Entities;

namespace AttendTrack.Domain.Interfaces.Repositories;

public interface IHourlySlotRepository
{
    Task<HourlySlot?> GetAsync(Guid attendanceRecordId, int hourSlot, CancellationToken ct = default);
    Task<IReadOnlyList<HourlySlot>> GetByAttendanceAsync(Guid attendanceRecordId, CancellationToken ct = default);
    Task<IReadOnlyList<HourlySlot>> GetByDateAsync(DateOnly workDate, CancellationToken ct = default);
    Task AddAsync(HourlySlot slot, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<HourlySlot> slots, CancellationToken ct = default);
    void Update(HourlySlot slot);
    /// <summary>Insert or update a slot — called by HourlyTrackerService every 60 s.</summary>
    Task UpsertAsync(HourlySlot slot, CancellationToken ct = default);
}
