using System.Net;
using System.Net.Sockets;

namespace AttendTrack.Web.Middleware;

/// <summary>
/// Restricts /kiosk and /api/kiosk/* routes to allowed IP CIDRs.
/// NEVER reads X-Forwarded-For — only context.Connection.RemoteIpAddress (Gap 3).
/// CIDR list is read from configuration: Kiosk:AllowedIpRanges = ["192.168.1.0/24", "10.0.0.0/8"]
/// </summary>
public sealed class TcpIpWhitelistMiddleware
{
    private static readonly string[] ProtectedPaths = { "/kiosk", "/api/kiosk" };

    private readonly RequestDelegate _next;
    private readonly ILogger<TcpIpWhitelistMiddleware> _logger;
    private readonly IReadOnlyList<(IPAddress network, int prefix)> _ranges;
    private readonly bool _enforced;

    public TcpIpWhitelistMiddleware(
        RequestDelegate next,
        IConfiguration  configuration,
        IWebHostEnvironment env,
        ILogger<TcpIpWhitelistMiddleware> logger)
    {
        _next   = next;
        _logger = logger;

        var raw = configuration.GetSection("Kiosk:AllowedIpRanges").Get<string[]>() ?? Array.Empty<string>();
        _ranges = raw.Select(Parse).Where(r => r.network != null).Cast<(IPAddress, int)>().ToList();

        // In Development with no ranges configured, do not enforce — local browser at 127.0.0.1 must work.
        _enforced = !env.IsDevelopment() || _ranges.Count > 0;

        if (_ranges.Count == 0)
            logger.LogInformation("TcpIpWhitelist: no Kiosk:AllowedIpRanges configured (enforced={E})", _enforced);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var matchesProtected = ProtectedPaths.Any(p =>
            path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        if (!matchesProtected || !_enforced)
        {
            await _next(context);
            return;
        }

        var remote = context.Connection.RemoteIpAddress;
        if (remote is not null && remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();

        if (remote is null || !_ranges.Any(r => InRange(remote, r.network, r.prefix)))
        {
            _logger.LogWarning("Kiosk path {Path} blocked from {IP}", path, remote);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Forbidden — kiosk endpoints are IP-restricted.");
            return;
        }

        await _next(context);
    }

    private static (IPAddress? network, int prefix) Parse(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2) return (null, 0);
        if (!IPAddress.TryParse(parts[0], out var ip)) return (null, 0);
        if (!int.TryParse(parts[1], out var prefix)) return (null, 0);
        return (ip, prefix);
    }

    private static bool InRange(IPAddress ip, IPAddress network, int prefix)
    {
        if (ip.AddressFamily != network.AddressFamily) return false;

        var ipBytes  = ip.GetAddressBytes();
        var netBytes = network.GetAddressBytes();
        if (ipBytes.Length != netBytes.Length) return false;

        var fullBytes = prefix / 8;
        var remainBits = prefix % 8;

        for (var i = 0; i < fullBytes; i++)
            if (ipBytes[i] != netBytes[i]) return false;

        if (remainBits == 0) return true;

        var mask = (byte)(0xFF << (8 - remainBits));
        return (ipBytes[fullBytes] & mask) == (netBytes[fullBytes] & mask);
    }
}
