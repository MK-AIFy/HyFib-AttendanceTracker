using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AttendTrack.Infrastructure.HealthChecks;

/// <summary>
/// Reports health of all registered Hikvision devices.
/// Healthy: all online. Degraded: some offline. Unhealthy: all offline.
/// </summary>
public sealed class HikvisionDeviceHealthCheck : IHealthCheck
{
    private readonly IHikvisionDeviceRepository _deviceRepo;
    private readonly IHikvisionIsapiService     _isapiService;

    public HikvisionDeviceHealthCheck(
        IHikvisionDeviceRepository deviceRepo,
        IHikvisionIsapiService     isapiService)
    {
        _deviceRepo   = deviceRepo;
        _isapiService = isapiService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var devices = await _deviceRepo.GetActiveDevicesAsync(cancellationToken);

        if (devices.Count == 0)
            return HealthCheckResult.Healthy("No active Hikvision devices registered.");

        var results = new List<(string Serial, bool Online)>();

        foreach (var device in devices)
        {
            var online = await _isapiService.PingDeviceAsync(device, cancellationToken);
            results.Add((device.SerialNumber, online));
        }

        var onlineCount  = results.Count(r => r.Online);
        var offlineCount = results.Count - onlineCount;

        var data = results.ToDictionary(
            r => r.Serial,
            r => (object)(r.Online ? "online" : "offline"));

        if (offlineCount == 0)
            return HealthCheckResult.Healthy($"All {onlineCount} device(s) online.", data);

        if (onlineCount > 0)
            return HealthCheckResult.Degraded(
                $"{offlineCount} device(s) offline, {onlineCount} online.", null, data);

        return HealthCheckResult.Unhealthy(
            $"All {offlineCount} Hikvision device(s) offline.", null, data);
    }
}
