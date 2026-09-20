using Microsoft.AspNetCore.Components.Server.Circuits;

namespace AttendTrack.Web.Security;

/// <summary>
/// Captures the RemoteIpAddress of the HTTP connection that opened this Blazor
/// Server circuit into the scoped KioskCircuitIpProvider. OnCircuitOpenedAsync
/// runs inside the /_blazor hub's OnConnectedAsync, which has a valid
/// HttpContext for the connecting client — unlike later component lifecycle
/// methods, where HttpContext is unreliable/null for a pure interactive circuit.
/// </summary>
public sealed class KioskCircuitHandler : CircuitHandler
{
    private readonly IHttpContextAccessor  _httpContextAccessor;
    private readonly KioskCircuitIpProvider _ipProvider;

    public KioskCircuitHandler(
        IHttpContextAccessor   httpContextAccessor,
        KioskCircuitIpProvider ipProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _ipProvider           = ipProvider;
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var remote = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        if (remote is not null && remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
        _ipProvider.RemoteIpAddress = remote;

        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }
}
