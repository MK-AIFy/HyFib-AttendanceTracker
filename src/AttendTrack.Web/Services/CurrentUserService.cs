using AttendTrack.Application.Common;
using System.Security.Claims;

namespace AttendTrack.Web.Services;

/// <summary>
/// Implements ICurrentUserService for the Web layer using IHttpContextAccessor.
/// Registered as scoped in DI.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Guid? UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Role
        => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);

    public string? IpAddress
        => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
