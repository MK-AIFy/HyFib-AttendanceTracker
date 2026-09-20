using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Web.Components;

/// <summary>
/// ErrorBoundary that logs the caught exception before switching to
/// ErrorContent. Without this, an unhandled exception anywhere in the routed
/// component tree (a page's OnInitializedAsync, an event handler, etc.) tears
/// down the entire Blazor Server circuit — the user sees a blank/disconnected
/// page with no way to recover short of a full reload, and nothing is logged.
/// </summary>
public sealed class LoggingErrorBoundary : ErrorBoundary
{
    [Inject] private ILogger<LoggingErrorBoundary> Logger { get; set; } = default!;

    protected override Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(exception, "Unhandled exception caught by circuit-level ErrorBoundary");
        return base.OnErrorAsync(exception);
    }
}
