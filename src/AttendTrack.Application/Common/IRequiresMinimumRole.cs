using AttendTrack.Domain.Enums;

namespace AttendTrack.Application.Common;

/// <summary>
/// Marker for MediatR requests that require the caller to hold at least the
/// given UserRole (lower enum ordinal = more privileged — see UserRole).
/// RoleAuthorizationBehaviour enforces this independently of any page-level
/// [Authorize(Roles=...)] attribute, since a Blazor component calling
/// Sender.Send in-process bypasses the ASP.NET Core authorization pipeline
/// that HTTP controller actions go through.
/// </summary>
public interface IRequiresMinimumRole
{
    UserRole MinimumRole { get; }
}
