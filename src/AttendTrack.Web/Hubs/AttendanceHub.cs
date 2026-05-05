using Microsoft.AspNetCore.SignalR;

namespace AttendTrack.Web.Hubs;

/// <summary>
/// SignalR hub for real-time attendance updates.
/// Admin pages subscribe to events via OnAttendanceChanged from AttendanceNotifier.
/// </summary>
public sealed class AttendanceHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admin");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admin");
        await base.OnDisconnectedAsync(exception);
    }
}

