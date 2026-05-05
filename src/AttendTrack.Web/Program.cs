using AttendTrack.Application;
using AttendTrack.Application.Common;
using AttendTrack.Infrastructure;
using AttendTrack.Infrastructure.Security;
using AttendTrack.Web.Hubs;
using AttendTrack.Web.Services;
using QuestPDF.Infrastructure;
using Serilog;

// QuestPDF Community license (free tier)
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

    // API Controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Rate limiting (Gap 11) — policies wired in Prompt 4
    builder.Services.AddRateLimiter(_ => { });

    // Application layer (MediatR + FluentValidation + AutoMapper + behaviours)
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

    // Health checks (Gap 10)
    builder.Services.AddHealthChecks();

    // CORS — locked to internal origin only
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins("https://attendtrack.local")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();

    // Middleware pipeline — order per CLAUDE.md
    // app.UseMiddleware<SecurityHeadersMiddleware>();   // Prompt 4
    app.UseRateLimiter();
    app.UseMiddleware<HikvisionWebhookAuthMiddleware>(); // IP + Basic auth
    // app.UseMiddleware<TcpIpWhitelistMiddleware>();    // Prompt 4
    app.UseAuthentication();
    app.UseAuthorization();
    // app.UseMiddleware<RequestAuditMiddleware>();      // Prompt 4
    // app.UseMiddleware<GlobalExceptionMiddleware>();   // Prompt 4

    app.MapControllers();
    app.MapBlazorHub();
    app.MapHub<AttendanceHub>("/hubs/attendance");
    app.MapHealthChecks("/health");
    app.MapFallbackToPage("/_Host");

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
