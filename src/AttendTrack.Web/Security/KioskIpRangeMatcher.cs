using System.Net;

namespace AttendTrack.Web.Security;

/// <summary>
/// Parses the Kiosk:AllowedIpRanges CIDR config once and matches remote IPs
/// against it. Shared by TcpIpWhitelistMiddleware (gates the initial HTTP GET
/// to /kiosk and /api/kiosk) and KioskCircuitIpProvider/KioskTerminal (gates
/// the Blazor Server circuit itself — see KioskCircuitHandler for why the
/// middleware alone isn't sufficient).
/// </summary>
public sealed class KioskIpRangeMatcher
{
    private readonly IReadOnlyList<(IPAddress network, int prefix)> _ranges;

    /// <summary>
    /// False only in Development with no ranges configured, so a local browser
    /// at 127.0.0.1 keeps working without extra setup.
    /// </summary>
    public bool Enforced { get; }

    public KioskIpRangeMatcher(IConfiguration configuration, IWebHostEnvironment env)
    {
        var raw = configuration.GetSection("Kiosk:AllowedIpRanges").Get<string[]>() ?? Array.Empty<string>();
        _ranges = raw.Select(Parse).Where(r => r.network != null).Cast<(IPAddress, int)>().ToList();

        Enforced = !env.IsDevelopment() || _ranges.Count > 0;
    }

    public bool IsAllowed(IPAddress? remote)
    {
        if (!Enforced) return true;
        if (remote is null) return false;

        if (remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
        return _ranges.Any(r => InRange(remote, r.network, r.prefix));
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

        var fullBytes  = prefix / 8;
        var remainBits = prefix % 8;

        for (var i = 0; i < fullBytes; i++)
            if (ipBytes[i] != netBytes[i]) return false;

        if (remainBits == 0) return true;

        var mask = (byte)(0xFF << (8 - remainBits));
        return (ipBytes[fullBytes] & mask) == (netBytes[fullBytes] & mask);
    }
}
