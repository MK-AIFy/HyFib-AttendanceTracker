using AttendTrack.Application.Common;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Application.Behaviours;

/// <summary>
/// Pipeline behaviour enforcing IRequiresMinimumRole for any request that
/// implements it. Both known callers of the Hikvision enroll/sync commands
/// go through an authenticated HTTP context (the admin API controller, whose
/// own [Authorize(Roles="SuperAdmin,Admin")] already covers it, and the
/// HikvisionDevices Blazor page, which had no such check — a Blazor
/// component's Sender.Send is an in-process MediatR call that never goes
/// through ASP.NET Core's authorization middleware, so the page-level
/// [Authorize(Roles=...)] attribute — deliberately broader, to let
/// HRManager/Manager view device status — did nothing to stop them from
/// invoking the same privileged commands as SuperAdmin/Admin).
/// </summary>
public sealed class RoleAuthorizationBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<RoleAuthorizationBehaviour<TRequest, TResponse>> _logger;

    public RoleAuthorizationBehaviour(
        ICurrentUserService                                            currentUser,
        ILogger<RoleAuthorizationBehaviour<TRequest, TResponse>>       logger)
    {
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 ct)
    {
        if (request is IRequiresMinimumRole restricted)
        {
            var hasRole = Enum.TryParse<UserRole>(_currentUser.Role, out var callerRole);
            if (!hasRole || (int)callerRole > (int)restricted.MinimumRole)
            {
                _logger.LogWarning(
                    "Access denied: {Request} requires {MinRole}, caller has {CallerRole}",
                    typeof(TRequest).Name, restricted.MinimumRole, _currentUser.Role ?? "(none)");
                throw new InsufficientRoleException(restricted.MinimumRole);
            }
        }

        return await next(ct).ConfigureAwait(false);
    }
}
