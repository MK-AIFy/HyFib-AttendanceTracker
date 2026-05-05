using AttendTrack.Domain.Entities;
using AttendTrack.Domain.ValueObjects;

namespace AttendTrack.Domain.Interfaces.Repositories;

public interface IAttendanceRepository
{
    Task<AttendanceRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AttendanceRecord?> GetByEmployeeAndDateAsync(EmployeeId employeeId, DateOnly workDate, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceRecord>> GetByDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceRecord>> GetByEmployeeAsync(EmployeeId employeeId, DateOnly from, DateOnly to, CancellationToken ct = default);
    /// <summary>Returns open check-ins (no checkout) older than the given UTC threshold — used by MissedPunchDetectorService.</summary>
    Task<IReadOnlyList<AttendanceRecord>> GetOpenCheckInsOlderThanAsync(DateTime thresholdUtc, CancellationToken ct = default);
    /// <summary>Returns all attendance records across a date range — used by report queries.</summary>
    Task<IReadOnlyList<AttendanceRecord>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task AddAsync(AttendanceRecord record, CancellationToken ct = default);
    void Update(AttendanceRecord record);
}
