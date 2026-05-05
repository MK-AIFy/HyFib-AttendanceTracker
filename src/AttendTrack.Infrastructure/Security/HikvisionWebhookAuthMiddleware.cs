using System.Text;
using AttendTrack.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Infrastructure.Security;

/// <summary>
/// Middleware that guards POST /api/hikvision/events.
/// Two-layer validation:
///   1. TCP RemoteIpAddress must be a registered, active Hikvision device IP.
///   2. HTTP Basic auth credentials must match the stored BCrypt hash.
///
/// NEVER reads X-Forwarded-For — uses only connection-level RemoteIpAddress (Gap 3).
/// Returns 403 for unknown IPs, 401 for bad credentials.
/// </summary>
public sealed class HikvisionWebhookAuthMiddleware
{
    private const string WebhookPath = "/api/hikvision/events";
    private readonly RequestDelegate _next;
    private readonly ILogger<HikvisionWebhookAuthMiddleware> _logger;

    public HikvisionWebhookAuthMiddleware(
        RequestDelegate next,
        ILogger<HikvisionWebhookAuthMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(WebhookPath,
                StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // ── Layer 1: TCP IP validation (NEVER X-Forwarded-For) ────────────────
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            _logger.LogWarning("Hikvision webhook: null RemoteIpAddress — rejecting");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // Normalise IPv4-mapped IPv6 (e.g. ::ffff:192.168.1.50 → 192.168.1.50)
        if (remoteIp.IsIPv4MappedToIPv6)
            remoteIp = remoteIp.MapToIPv4();

        var ipStr = remoteIp.ToString();

        // Resolve scoped repository from DI (middleware is singleton)
        var deviceRepo = context.RequestServices
            .GetRequiredService<IHikvisionDeviceRepository>();

        var device = await deviceRepo.GetByIpAsync(ipStr, context.RequestAborted);
        if (device is null)
        {
            _logger.LogWarning(
                "Hikvision webhook: request from unregistered IP {IP} — 403", ipStr);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // ── Layer 2: HTTP Basic auth validation ───────────────────────────────
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!ValidateBasicAuth(authHeader, device.AdminUsername, device.AdminPasswordHash))
        {
            _logger.LogWarning(
                "Hikvision webhook: invalid Basic auth from {IP} — 401", ipStr);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }

    private static bool ValidateBasicAuth(string? authHeader, string username, string passwordHash)
    {
        if (string.IsNullOrEmpty(authHeader)
            || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(
                Convert.FromBase64String(authHeader["Basic ".Length..]));
        }
        catch (FormatException)
        {
            return false;
        }

        var colonIdx = decoded.IndexOf(':', StringComparison.Ordinal);
        if (colonIdx < 1) return false;

        var incomingUser = decoded[..colonIdx];
        var incomingPass = decoded[(colonIdx + 1)..];

        if (incomingUser != username) return false;

        return BCrypt.Net.BCrypt.Verify(incomingPass, passwordHash);
    }
}
