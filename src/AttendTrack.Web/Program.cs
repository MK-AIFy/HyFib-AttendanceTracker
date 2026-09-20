using AttendTrack.Application;
using AttendTrack.Application.Common;
using AttendTrack.Application.Options;
using AttendTrack.Infrastructure;
using AttendTrack.Infrastructure.Persistence;
using AttendTrack.Infrastructure.Security;
using AttendTrack.Web.Hubs;
using AttendTrack.Web.Middleware;
using AttendTrack.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using Serilog;
using System.Text;

QuestPDF.Settings.License = LicenseType.Community;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/attendtrack-.txt", rollingInterval: RollingInterval.Day));

    // Blazor Server
    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();

    // Kiosk IP whitelist — shared CIDR matcher plus a CircuitHandler that captures
    // each circuit's originating IP, so /kiosk can be IP-gated even when reached
    // via in-circuit client-side navigation (see KioskCircuitHandler).
    builder.Services.AddSingleton<AttendTrack.Web.Security.KioskIpRangeMatcher>();
    builder.Services.AddScoped<AttendTrack.Web.Security.KioskCircuitIpProvider>();
    builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler,
        AttendTrack.Web.Security.KioskCircuitHandler>();

    // API Controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Rate limiting (Gap 11)
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Login + kiosk auth — 10 req/min/IP
        options.AddPolicy("kiosk-auth", ctx =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window      = TimeSpan.FromMinutes(1),
                    QueueLimit  = 0,
                }));

        // Hikvision webhook — 200 req/min/IP
        options.AddPolicy("webhook", ctx =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window      = TimeSpan.FromMinutes(1),
                    QueueLimit  = 0,
                }));

        // Default API — 60 req/min/user (or IP if anonymous)
        options.AddPolicy("api-default", ctx =>
        {
            var key = ctx.User?.Identity?.IsAuthenticated == true
                ? ctx.User.Identity.Name ?? "user"
                : ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(key,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window      = TimeSpan.FromMinutes(1),
                    QueueLimit  = 0,
                });
        });
    });

    // Authentication: cookie for Blazor UI, JWT bearer for /api/* clients
    var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
    var jwtSecret  = jwtSection["SecretKey"] ?? "";
    if (jwtSecret.Length < 32)
        throw new InvalidOperationException("Jwt:SecretKey must be at least 32 chars.");

    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, opts =>
        {
            opts.Cookie.Name         = "AttendTrack.Auth";
            opts.Cookie.HttpOnly     = true;
            opts.Cookie.SameSite     = SameSiteMode.Lax;
            opts.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            opts.LoginPath           = "/login";
            opts.LogoutPath          = "/auth/logout";
            opts.AccessDeniedPath    = "/login";
            opts.ExpireTimeSpan      = TimeSpan.FromHours(8);
            opts.SlidingExpiration   = true;
            opts.Events.OnRedirectToLogin = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api") ||
                    ctx.Request.Path.StartsWithSegments("/auth"))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }
                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            };
        })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = jwtSection["Issuer"],
                ValidAudience            = jwtSection["Audience"],
                IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew                = TimeSpan.FromMinutes(2),
            };
        });

    builder.Services.AddAuthorization(opts =>
    {
        // Default policy honors EITHER auth scheme — cookie for Blazor UI,
        // JWT bearer for /api/* clients. Without this [Authorize] only checks
        // the cookie scheme and JWT-authenticated callers get 401.
        opts.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
                CookieAuthenticationDefaults.AuthenticationScheme,
                JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .Build();
    });

    // Application layer
    builder.Services.AddApplication();

    // Infrastructure services
    builder.Services.AddInfrastructure(builder.Configuration);

    // Web-layer services
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
    builder.Services.AddSingleton<AttendanceNotifier>();
    builder.Services.AddSingleton<IAttendanceNotifier>(sp =>
        sp.GetRequiredService<AttendanceNotifier>());

    // SignalR
    builder.Services.AddSignalR();

    // Health checks
    builder.Services.AddHealthChecks();

    // CORS — env-aware origins
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
        {
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                          ?? (builder.Environment.IsDevelopment()
                              ? new[] { "http://localhost:5004", "https://localhost:5004",
                                        "http://localhost:5000", "https://localhost:5001" }
                              : new[] { "https://attendtrack.local" });
            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }));

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    // GlobalException is outermost so it wraps everything below.
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();

    app.UseStaticFiles();
    app.UseRouting();

    app.UseRateLimiter();
    app.UseMiddleware<HikvisionWebhookAuthMiddleware>();
    app.UseMiddleware<TcpIpWhitelistMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<RequestAuditMiddleware>();

    app.MapControllers();
    app.MapRazorPages();
    app.MapBlazorHub();
    app.MapHub<AttendanceHub>("/hubs/attendance");
    app.MapHealthChecks("/health");
    app.MapFallbackToPage("/_Host");

    // Seed reference data on first run
    await DbInitializer.SeedAsync(app.Services);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AttendTrack failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
