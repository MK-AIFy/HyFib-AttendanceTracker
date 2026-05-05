namespace AttendTrack.Application.Common;

/// <summary>
/// Abstraction for the currently authenticated user.
/// Implemented in AttendTrack.Web using IHttpContextAccessor.
/// Injected into AuditBehaviour to capture actor identity.
/// </summary>
public interface ICurrentUserService
{
    Guid?   UserId    { get; }
    string? Role      { get; }
    string? IpAddress { get; }
}
