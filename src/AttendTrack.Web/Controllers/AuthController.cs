using System.Security.Claims;
using AttendTrack.Application.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AttendTrack.Web.Controllers;

[ApiController]
[Microsoft.AspNetCore.Mvc.Route("auth")]
public sealed class AuthController : ControllerBase
{
    private const int    MaxFailedAttempts = 5;
    private const int    LockoutMinutes    = 15;
    private const string LockoutKeyPrefix  = "auth:lockout:";
    private const string AttemptsKeyPrefix = "auth:attempts:";

    private readonly IEmployeeRepository      _employees;
    private readonly IJwtTokenService         _jwt;
    private readonly IDistributedCacheWrapper _cache;
    private readonly ILogger<AuthController>  _logger;

    public AuthController(
        IEmployeeRepository      employees,
        IJwtTokenService         jwt,
        IDistributedCacheWrapper cache,
        ILogger<AuthController>  logger)
    {
        _employees = employees;
        _jwt       = jwt;
        _cache     = cache;
        _logger    = logger;
    }

    public sealed record LoginRequest(string EmployeeCode, string Pin);

    public sealed record LoginResponse(
        Guid     UserId,
        string   EmployeeCode,
        string   FullName,
        string   Role,
        string   Token,
        DateTime ExpiresAtUtc);

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("kiosk-auth")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeCode) ||
            string.IsNullOrWhiteSpace(request.Pin))
        {
            return BadRequest(new { error = "Employee code and PIN are required." });
        }

        var lockoutKey  = LockoutKeyPrefix + request.EmployeeCode.ToUpperInvariant();
        var lockedUntil = await _cache.GetAsync<DateTime?>(lockoutKey, ct).ConfigureAwait(false);
        if (lockedUntil.HasValue && lockedUntil.Value > DateTime.UtcNow)
        {
            return StatusCode(429, new
            {
                error      = "Account temporarily locked. Try again later.",
                retryAfter = (int)(lockedUntil.Value - DateTime.UtcNow).TotalSeconds
            });
        }

        var employee = await _employees.GetByCodeAsync(request.EmployeeCode, ct)
            .ConfigureAwait(false);

        if (employee is null || !employee.IsActive)
        {
            await RecordFailureAsync(request.EmployeeCode, ct).ConfigureAwait(false);
            _logger.LogWarning("Login failed: unknown or inactive code {Code}", request.EmployeeCode);
            return Unauthorized(new { error = "Invalid employee code or PIN." });
        }

        if (!PinHasher.Verify(request.Pin, employee.KioskPin.Value))
        {
            await RecordFailureAsync(request.EmployeeCode, ct).ConfigureAwait(false);
            _logger.LogWarning("Login failed: bad PIN for {Code}", request.EmployeeCode);
            return Unauthorized(new { error = "Invalid employee code or PIN." });
        }

        await _cache.RemoveAsync(AttemptsKeyPrefix + request.EmployeeCode.ToUpperInvariant(), ct)
            .ConfigureAwait(false);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, employee.Id.Value.ToString()),
            new(ClaimTypes.Name,           employee.FullName),
            new(ClaimTypes.Email,          employee.Email),
            new(ClaimTypes.Role,           employee.Role.ToString()),
            new("employee_code",           employee.EmployeeCode),
        };

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc   = DateTimeOffset.UtcNow.AddHours(8),
            }).ConfigureAwait(false);

        var (token, expiresAt) = _jwt.Issue(employee);

        _logger.LogInformation("User {Code} logged in", employee.EmployeeCode);
        return Ok(new LoginResponse(
            UserId:       employee.Id.Value,
            EmployeeCode: employee.EmployeeCode,
            FullName:     employee.FullName,
            Role:         employee.Role.ToString(),
            Token:        token,
            ExpiresAtUtc: expiresAt));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        return Ok(new { status = "logged_out" });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        userId       = User.FindFirstValue(ClaimTypes.NameIdentifier),
        name         = User.FindFirstValue(ClaimTypes.Name),
        email        = User.FindFirstValue(ClaimTypes.Email),
        role         = User.FindFirstValue(ClaimTypes.Role),
        employeeCode = User.FindFirstValue("employee_code"),
    });

    private async Task RecordFailureAsync(string employeeCode, CancellationToken ct)
    {
        var code        = employeeCode.ToUpperInvariant();
        var attemptsKey = AttemptsKeyPrefix + code;
        var attempts    = await _cache.GetAsync<int>(attemptsKey, ct).ConfigureAwait(false);
        attempts++;

        await _cache.SetAsync(attemptsKey, attempts, TimeSpan.FromMinutes(LockoutMinutes), ct)
            .ConfigureAwait(false);

        if (attempts >= MaxFailedAttempts)
        {
            var until = DateTime.UtcNow.AddMinutes(LockoutMinutes);
            await _cache.SetAsync(LockoutKeyPrefix + code, (DateTime?)until,
                TimeSpan.FromMinutes(LockoutMinutes), ct).ConfigureAwait(false);
            _logger.LogWarning("Account {Code} locked until {Until} after {Attempts} failures",
                code, until, attempts);
        }
    }
}
