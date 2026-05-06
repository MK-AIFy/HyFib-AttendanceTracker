using System.Collections.Concurrent;
using AttendTrack.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Infrastructure.Services;

/// <summary>
/// [Gap 12] Tracks the set of Blazor PIN kiosks that have heart-beated at least
/// once since the app started. Exposed to <see cref="KioskOfflineAlertService"/>
/// (and admin UI later) so we can warn when a kiosk goes silent.
/// In-memory only — authoritative state lives in Redis under
/// <c>kiosk:heartbeat:{deviceId}</c> (90s TTL).
/// </summary>
public sealed class KioskRegistry
{
    private readonly ConcurrentDictionary<string, DateTime> _seen = new();

    public void RecordSeen(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;
        _seen[deviceId] = DateTime.UtcNow;
    }

    public IReadOnlyCollection<string> KnownDeviceIds => _seen.Keys.ToArray();
}

/// <summary>
/// [Gap 12] Every 60s, checks each known kiosk's heartbeat key. If absent (TTL
/// expired without a fresh heartbeat) the kiosk is considered offline and we
/// log a warning. Real-world: extend this to email/SignalR-broadcast.
/// </summary>
public sealed class KioskOfflineAlertService : BackgroundService
{
    private const string HeartbeatKeyPrefix = "kiosk:heartbeat:";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory               _scopeFactory;
    private readonly KioskRegistry                      _registry;
    private readonly ILogger<KioskOfflineAlertService>  _logger;
    private readonly HashSet<string>                    _alreadyAlerted = new(StringComparer.OrdinalIgnoreCase);

    public KioskOfflineAlertService(
        IServiceScopeFactory                scopeFactory,
        KioskRegistry                       registry,
        ILogger<KioskOfflineAlertService>   logger)
    {
        _scopeFactory = scopeFactory;
        _registry     = registry;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KioskOfflineAlertService started (interval: {Sec}s)", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckHeartbeatsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "KioskOfflineAlertService unexpected error");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task CheckHeartbeatsAsync(CancellationToken ct)
    {
        var ids = _registry.KnownDeviceIds;
        if (ids.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCacheWrapper>();

        foreach (var id in ids)
        {
            // We just need to know if the key exists — payload type doesn't matter.
            var beacon = await cache.GetAsync<object>(HeartbeatKeyPrefix + id, ct);
            if (beacon is null)
            {
                if (_alreadyAlerted.Add(id))
                    _logger.LogWarning("Kiosk OFFLINE: {DeviceId} (no heartbeat in last 90s)", id);
            }
            else
            {
                if (_alreadyAlerted.Remove(id))
                    _logger.LogInformation("Kiosk back ONLINE: {DeviceId}", id);
            }
        }
    }
}
