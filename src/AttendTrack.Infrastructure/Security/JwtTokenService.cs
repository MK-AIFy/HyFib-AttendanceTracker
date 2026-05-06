using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AttendTrack.Application.Interfaces;
using AttendTrack.Application.Options;
using AttendTrack.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AttendTrack.Infrastructure.Security;

/// <summary>
/// Issues HS256-signed JWTs for API clients.
/// Cookie auth handles the Blazor UI; this complements it for /api/* consumers.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _opts;

    public JwtTokenService(IOptions<JwtOptions> opts)
    {
        _opts = opts.Value;
        if (string.IsNullOrWhiteSpace(_opts.SecretKey) || _opts.SecretKey.Length < 32)
            throw new InvalidOperationException(
                "Jwt:SecretKey must be configured and at least 32 characters long.");
    }

    public (string Token, DateTime ExpiresAtUtc) Issue(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        var expiresAt = DateTime.UtcNow.AddMinutes(_opts.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, employee.Id.Value.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier,   employee.Id.Value.ToString()),
            new(ClaimTypes.Name,             employee.FullName),
            new(ClaimTypes.Email,            employee.Email),
            new(ClaimTypes.Role,             employee.Role.ToString()),
            new("employee_code",             employee.EmployeeCode),
        };

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             _opts.Issuer,
            audience:           _opts.Audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            expiresAt,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
