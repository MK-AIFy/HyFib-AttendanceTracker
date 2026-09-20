using System.Net;

namespace AttendTrack.Web.Security;

/// <summary>
/// Scoped per Blazor Server circuit (one DI scope per SignalR connection).
/// Holds the RemoteIpAddress captured when the circuit was opened — see
/// KioskCircuitHandler. This is the address that actually matters for kiosk
/// IP restriction: it reflects the real connecting client regardless of
/// whether the browser later navigates to /kiosk via a fresh HTTP GET or via
/// in-circuit client-side routing (which issues no HTTP request at all, so
/// TcpIpWhitelistMiddleware never runs for it).
/// </summary>
public sealed class KioskCircuitIpProvider
{
    public IPAddress? RemoteIpAddress { get; set; }
}
