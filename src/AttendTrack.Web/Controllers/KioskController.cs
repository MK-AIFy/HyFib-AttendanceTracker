using AttendTrack.Application.Interfaces;
using AttendTrack.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AttendTrack.Web.Controllers;

/// <summary>
/// [Gap 12] Kiosk heartbeat endpoint. Each Blazor PIN kiosk POSTs every 30s.
/// We write to <c>kiosk:heartbeat:{deviceId}</c> with 90s TTL via
/// <see cref="IDistributedCacheWrapper"/>. <c>KioskOfflineAlertService</c>
/// scans every 60s — if the key is missing the kiosk is considered offline.
/// </summary>
[ApiController]
[Microsoft.AspNetCore.Mvc.Route("api/kiosk")]
public sealed class KioskController : ControllerBase
{
    public const string HeartbeatKeyPrefix = "kiosk:heartbeat:";
    private static readonly TimeSpan HeartbeatTtl = TimeSpan.FromSeconds(90);

    private readonly IDistributedCacheWrapper _cache;
    private readonly KioskRegistry            _registry;
    private readonly ILogger<KioskController>  _logger;

    public KioskController(
        IDistributedCacheWrapper cache,
        KioskRegistry            registry,
        ILogger<KioskController> logger)
    {
        _cache    = cache;
        _registry = registry;
        _logger   = logger;
    }

    public sealed record HeartbeatRequest(string DeviceId, string? Location);
    public sealed record HeartbeatBeacon(string DeviceId, string? Location, DateTime LastSeenUtc);

    [HttpPost("heartbeat")]
    [AllowAnonymous]
    [EnableRateLimiting("api-default")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DeviceId))
            return BadRequest(new { error = "DeviceId is required" });

        var beacon = new HeartbeatBeacon(req.DeviceId, req.Location, DateTime.UtcNow);
        await _cache.SetAsync(HeartbeatKeyPrefix + req.DeviceId, beacon, HeartbeatTtl, ct);
        _registry.RecordSeen(req.DeviceId);

        _logger.LogDebug("Kiosk heartbeat from {DeviceId} ({Location})", req.DeviceId, req.Location);
        return Ok(new { status = "ok", lastSeenUtc = beacon.LastSeenUtc });
    }
}
