using AttendTrack.Web.Security;

namespace AttendTrack.Web.Middleware;

/// <summary>
/// Restricts /kiosk and /api/kiosk/* routes to allowed IP CIDRs.
/// NEVER reads X-Forwarded-For — only context.Connection.RemoteIpAddress (Gap 3).
/// CIDR list is read from configuration: Kiosk:AllowedIpRanges = ["192.168.1.0/24", "10.0.0.0/8"]
///
/// This only gates the initial HTTP GET to a protected path. It does NOT gate a
/// Blazor Server circuit reaching /kiosk afterwards via the shared /_blazor
/// SignalR hub or via in-circuit client-side navigation — see
/// KioskCircuitHandler/KioskCircuitIpProvider for the component-level check
/// that closes that gap.
/// </summary>
public sealed class TcpIpWhitelistMiddleware
{
    private static readonly string[] ProtectedPaths = { "/kiosk", "/api/kiosk" };

    private readonly RequestDelegate _next;
    private readonly KioskIpRangeMatcher _matcher;
    private readonly ILogger<TcpIpWhitelistMiddleware> _logger;

    public TcpIpWhitelistMiddleware(
        RequestDelegate                    next,
        KioskIpRangeMatcher                 matcher,
        ILogger<TcpIpWhitelistMiddleware>  logger)
    {
        _next    = next;
        _matcher = matcher;
        _logger  = logger;

        if (!matcher.Enforced)
            logger.LogInformation("TcpIpWhitelist: no Kiosk:AllowedIpRanges configured — not enforced");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var matchesProtected = ProtectedPaths.Any(p =>
            path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        if (!matchesProtected || !_matcher.Enforced)
        {
            await _next(context);
            return;
        }

        if (!_matcher.IsAllowed(context.Connection.RemoteIpAddress))
        {
            _logger.LogWarning("Kiosk path {Path} blocked from {IP}", path, context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Forbidden — kiosk endpoints are IP-restricted.");
            return;
        }

        await _next(context);
    }
}
