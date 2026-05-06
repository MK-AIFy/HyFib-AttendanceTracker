using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AttendTrack.Application.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace AttendTrack.Web.Pages;

[AllowAnonymous]
[EnableRateLimiting("kiosk-auth")]
public sealed class LoginModel : PageModel
{
    private const int    MaxFailedAttempts = 5;
    private const int    LockoutMinutes    = 15;
    private const string LockoutKeyPrefix  = "auth:lockout:";
    private const string AttemptsKeyPrefix = "auth:attempts:";

    private readonly IEmployeeRepository      _employees;
    private readonly IDistributedCacheWrapper _cache;
    private readonly ILogger<LoginModel>      _logger;

    public LoginModel(
        IEmployeeRepository      employees,
        IDistributedCacheWrapper cache,
        ILogger<LoginModel>      logger)
    {
        _employees = employees;
        _cache     = cache;
        _logger    = logger;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public sealed class InputModel
    {
        [Required, Display(Name = "Employee Code")]
        public string EmployeeCode { get; set; } = "";

        [Required, Display(Name = "PIN")]
        [DataType(DataType.Password)]
        [StringLength(6, MinimumLength = 4)]
        public string Pin { get; set; } = "";
    }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return LocalRedirect("/admin/dashboard");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Page();

        var code        = Input.EmployeeCode.Trim();
        var lockoutKey  = LockoutKeyPrefix + code.ToUpperInvariant();
        var lockedUntil = await _cache.GetAsync<DateTime?>(lockoutKey, ct).ConfigureAwait(false);

        if (lockedUntil.HasValue && lockedUntil.Value > DateTime.UtcNow)
        {
            ErrorMessage = "Account temporarily locked. Try again in a few minutes.";
            return Page();
        }

        var employee = await _employees.GetByCodeAsync(code, ct).ConfigureAwait(false);

        if (employee is null || !employee.IsActive ||
            !PinHasher.Verify(Input.Pin.Trim(), employee.KioskPin.Value))
        {
            await RecordFailureAsync(code, ct).ConfigureAwait(false);
            _logger.LogWarning("Login failed for {Code}", code);
            ErrorMessage = "Invalid employee code or PIN.";
            return Page();
        }

        await _cache.RemoveAsync(AttemptsKeyPrefix + code.ToUpperInvariant(), ct)
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

        _logger.LogInformation("User {Code} logged in", employee.EmployeeCode);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        // Role-aware default landing
        var role = employee.Role.ToString();
        var isAdminRole = role is "SuperAdmin" or "Admin" or "HRManager" or "Manager";
        return LocalRedirect(isAdminRole ? "/admin/dashboard" : "/employee/my-attendance");
    }

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
            _logger.LogWarning("Account {Code} locked until {Until}", code, until);
        }
    }
}
