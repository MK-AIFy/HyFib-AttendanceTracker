using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Infrastructure.Security;

/// <summary>
/// [Gap 3] Kiosk IP whitelist middleware.
/// Restricts access to /kiosk/* paths to admin-office IP addresses only.
/// NEVER reads X-Forwarded-For — only uses TCP RemoteIpAddress to prevent spoofing.
/// </summary>
public sealed class TcpIpWhitelistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TcpIpWhitelistMiddleware> _logger;
    private readonly IReadOnlySet<string> _allowedIps;

    public TcpIpWhitelistMiddleware(
        RequestDelegate                    next,
        IConfiguration                     configuration,
        ILogger<TcpIpWhitelistMiddleware>  logger)
    {
        _next   = next;
        _logger = logger;

        // Reads e.g. "KioskAllowedIps": ["192.168.1.10", "192.168.1.11"]
        var ips = configuration.GetSection("KioskAllowedIps").Get<string[]>() ?? [];
        _allowedIps = new HashSet<string>(ips, StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only restrict kiosk routes — all other routes (API, admin) have their own auth
        if (!context.Request.Path.StartsWithSegments("/kiosk"))
        {
            await _next(context);
            return;
        }

        // CRITICAL: Use only TCP RemoteIpAddress — never X-Forwarded-For [Gap 3]
        var remoteIp = context.Connection.RemoteIpAddress;

        // Normalise IPv6-mapped IPv4 (e.g. ::ffff:192.168.1.10 → 192.168.1.10)
        if (remoteIp?.IsIPv4MappedToIPv6 == true)
            remoteIp = remoteIp.MapToIPv4();

        var remoteIpStr = remoteIp?.ToString() ?? string.Empty;

        // If no whitelist configured, allow all (development mode)
        if (_allowedIps.Count == 0 || _allowedIps.Contains(remoteIpStr))
        {
            await _next(context);
            return;
        }

        _logger.LogWarning(
            "Kiosk access denied: IP {IP} not in whitelist. Path: {Path}",
            remoteIpStr, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
    }
}

