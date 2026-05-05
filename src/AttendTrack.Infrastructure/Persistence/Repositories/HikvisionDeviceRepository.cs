using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AttendTrack.Infrastructure.Persistence.Repositories;

public sealed class HikvisionDeviceRepository : IHikvisionDeviceRepository
{
    private readonly AttendTrackDbContext _db;
    public HikvisionDeviceRepository(AttendTrackDbContext db) => _db = db;

    public async Task<HikvisionDevice?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.HikvisionDevices.FindAsync([id], ct);

    public async Task<HikvisionDevice?> GetByIpAsync(string ipAddress, CancellationToken ct)
        => await _db.HikvisionDevices
            .FirstOrDefaultAsync(d => d.IpAddress == ipAddress && d.IsActive, ct);

    public async Task<HikvisionDevice?> GetBySerialAsync(string serialNumber, CancellationToken ct)
        => await _db.HikvisionDevices
            .FirstOrDefaultAsync(d => d.SerialNumber == serialNumber, ct);

    public async Task<IReadOnlyList<HikvisionDevice>> GetActiveDevicesAsync(CancellationToken ct)
        => await _db.HikvisionDevices
            .Where(d => d.IsActive)
            .ToListAsync(ct);

    public async Task AddAsync(HikvisionDevice device, CancellationToken ct)
        => await _db.HikvisionDevices.AddAsync(device, ct);

    public void Update(HikvisionDevice device)
        => _db.HikvisionDevices.Update(device);

    // ── Event log operations ────────────────────────────────────────────────

    public async Task AddEventLogAsync(HikvisionEventLog eventLog, CancellationToken ct)
        => await _db.HikvisionEventLogs.AddAsync(eventLog, ct);

    public async Task<bool> EventLogExistsAsync(
        string deviceSerial, DateTime deviceLocalTime, string employeeCode, CancellationToken ct)
        => await _db.HikvisionEventLogs.AnyAsync(
            e => e.DeviceSerialNumber == deviceSerial
              && e.DeviceLocalTime    == deviceLocalTime
              && e.EmployeeCode       == employeeCode, ct);

    public async Task UpdateLastEventAsync(Guid deviceId, DateTime eventTime, CancellationToken ct)
    {
        var device = await _db.HikvisionDevices.FindAsync([deviceId], ct);
        device?.RecordEventReceived(eventTime);
    }

    public async Task UpdateLastPollAsync(Guid deviceId, DateTime pollTime, CancellationToken ct)
    {
        var device = await _db.HikvisionDevices.FindAsync([deviceId], ct);
        device?.RecordPoll(pollTime);
    }

    public async Task<IReadOnlyList<HikvisionEventLog>> GetRecentEventLogsAsync(
        string deviceSerialNumber, int maxCount, CancellationToken ct)
        => await _db.HikvisionEventLogs
            .Where(e => e.DeviceSerialNumber == deviceSerialNumber)
            .OrderByDescending(e => e.ReceivedAtUtc)
            .Take(maxCount)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<HikvisionEventLog>> GetUnprocessedEventLogsAsync(CancellationToken ct)
        => await _db.HikvisionEventLogs
            .Where(e => !e.IsProcessed)
            .OrderBy(e => e.ReceivedAtUtc)
            .ToListAsync(ct);
}
