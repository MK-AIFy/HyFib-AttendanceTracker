using AttendTrack.Domain.Entities;

namespace AttendTrack.Domain.Interfaces.Repositories;

public interface IHikvisionDeviceRepository
{
    Task<HikvisionDevice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<HikvisionDevice?> GetByIpAsync(string ipAddress, CancellationToken ct = default);
    Task<HikvisionDevice?> GetBySerialAsync(string serialNumber, CancellationToken ct = default);
    Task<IReadOnlyList<HikvisionDevice>> GetActiveDevicesAsync(CancellationToken ct = default);
    Task AddAsync(HikvisionDevice device, CancellationToken ct = default);
    void Update(HikvisionDevice device);

    // --- Event log operations ---
    Task AddEventLogAsync(HikvisionEventLog eventLog, CancellationToken ct = default);
    /// <summary>Returns true if a log entry already exists for the dedup key (deviceSerial, deviceLocalTime, employeeCode).</summary>
    Task<bool> EventLogExistsAsync(string deviceSerial, DateTime deviceLocalTime, string employeeCode, CancellationToken ct = default);
    Task UpdateLastEventAsync(Guid deviceId, DateTime eventTime, CancellationToken ct = default);
    Task UpdateLastPollAsync(Guid deviceId, DateTime pollTime, CancellationToken ct = default);
    /// <summary>Returns the most recent event logs for a device (ordered newest first).</summary>
    Task<IReadOnlyList<HikvisionEventLog>> GetRecentEventLogsAsync(string deviceSerialNumber, int maxCount, CancellationToken ct = default);
    /// <summary>Returns all unprocessed event logs for retry/monitoring.</summary>
    Task<IReadOnlyList<HikvisionEventLog>> GetUnprocessedEventLogsAsync(CancellationToken ct = default);
}
