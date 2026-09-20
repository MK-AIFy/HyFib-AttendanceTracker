using AttendTrack.Application.Common;
using AttendTrack.Domain.Enums;
using MediatR;

namespace AttendTrack.Application.Commands.Hikvision;

/// <summary>
/// Manually triggers a polling sync for a specific Hikvision device.
/// Used by admin [Sync Now] button and HikvisionEventController's manual-sync
/// endpoint (HikvisionPollingService's own background poll goes through
/// ProcessHikvisionEventCommand directly, not this command).
/// SuperAdmin/Admin only — matches HikvisionEventController.SyncDevice's
/// equivalent [Authorize(Roles="SuperAdmin,Admin")] (RoleAuthorizationBehaviour).
/// </summary>
public sealed record SyncDeviceEventsCommand(
    Guid     DeviceId,
    DateTime SinceUtc) : IRequest<SyncDeviceEventsResult>, IRequiresMinimumRole
{
    public UserRole MinimumRole => UserRole.Admin;
}

public sealed record SyncDeviceEventsResult(
    Guid DeviceId,
    int  EventsProcessed,
    int  EventsSkipped);
