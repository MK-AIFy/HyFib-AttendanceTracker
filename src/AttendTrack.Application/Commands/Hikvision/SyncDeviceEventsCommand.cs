using MediatR;

namespace AttendTrack.Application.Commands.Hikvision;

/// <summary>
/// Manually triggers a polling sync for a specific Hikvision device.
/// Used by admin [Sync Now] button and by HikvisionPollingService.
/// </summary>
public sealed record SyncDeviceEventsCommand(
    Guid     DeviceId,
    DateTime SinceUtc) : IRequest<SyncDeviceEventsResult>;

public sealed record SyncDeviceEventsResult(
    Guid DeviceId,
    int  EventsProcessed,
    int  EventsSkipped);
