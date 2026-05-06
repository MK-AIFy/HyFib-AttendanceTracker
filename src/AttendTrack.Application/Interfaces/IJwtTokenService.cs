using AttendTrack.Domain.Entities;

namespace AttendTrack.Application.Interfaces;

/// <summary>
/// Issues signed JWT bearer tokens for API clients (Hikvision admin tools, mobile, integrations).
/// Cookie auth is used for the Blazor admin UI; this service is the secondary path.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>Returns (token, expiresAtUtc).</summary>
    (string Token, DateTime ExpiresAtUtc) Issue(Employee employee);
}
