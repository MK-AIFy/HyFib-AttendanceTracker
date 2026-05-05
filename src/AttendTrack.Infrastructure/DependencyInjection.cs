using AttendTrack.Application.Common;
using AttendTrack.Application.Interfaces;
using AttendTrack.Application.Options;
using AttendTrack.Domain.Interfaces;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.Interfaces.Services;
using AttendTrack.Infrastructure.HealthChecks;
using AttendTrack.Infrastructure.Persistence;
using AttendTrack.Infrastructure.Persistence.Repositories;
using AttendTrack.Infrastructure.Security;
using AttendTrack.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AttendTrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        // ── Options ───────────────────────────────────────────────────────────
        services.Configure<HikvisionOptions>(
            configuration.GetSection(HikvisionOptions.SectionName));

        // ── EF Core + PostgreSQL ──────────────────────────────────────────────
        services.AddDbContext<AttendTrackDbContext>(opts =>
            opts.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(
                    typeof(AttendTrackDbContext).Assembly.FullName)));

        // ── Unit of Work ──────────────────────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Repositories ──────────────────────────────────────────────────────
        services.AddScoped<IEmployeeRepository,     EmployeeRepository>();
        services.AddScoped<IAttendanceRepository,   AttendanceRepository>();
        services.AddScoped<IShiftRepository,        ShiftRepository>();
        services.AddScoped<IHourlySlotRepository,   HourlySlotRepository>();
        services.AddScoped<IAuditRepository,        AuditRepository>();
        services.AddScoped<IHikvisionDeviceRepository, HikvisionDeviceRepository>();

        // ── Password Hasher (BCrypt, work factor 12) ──────────────────────────
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        // ── Data Protection (for ISAPI credential encryption) ─────────────────
        services.AddDataProtection()
            .SetApplicationName("AttendTrack");

        // ── Hikvision ISAPI Service ───────────────────────────────────────────
        services.AddScoped<IHikvisionIsapiService, HikvisionIsapiService>();

        // Register Func<string, string> for password protection
        // Used by RegisterHikvisionDeviceHandler to encrypt the admin password
        services.AddScoped<Func<string, string>>(sp =>
        {
            var isapiService = sp.GetRequiredService<HikvisionIsapiService>();
            return isapiService.ProtectPassword;
        });

        // Also register the concrete type for the above to resolve
        services.AddScoped<HikvisionIsapiService>();

        // ── [Gap 7] Redis + Distributed Cache Wrapper ─────────────────────────
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(opts =>
                opts.Configuration = redisConnectionString);
        }
        else
        {
            // Development fallback: use in-memory distributed cache
            services.AddDistributedMemoryCache();
        }
        services.AddMemoryCache();
        services.AddSingleton<IDistributedCacheWrapper, DistributedCacheWrapper>();

        // ── [Gap 6] DPDP Data Subject Service ────────────────────────────────
        services.AddScoped<IDataSubjectService, DataSubjectService>();
        services.AddScoped<IReportExportService, ReportExportService>();

        // ── Background Services ───────────────────────────────────────────────
        services.AddHostedService<HikvisionPollingService>();
        services.AddHostedService<HourlyTrackerService>();          // [Gap 4]
        services.AddHostedService<MissedPunchDetectorService>();    // [Gap 1]
        services.AddHostedService<DataRetentionPurgeService>();     // [Gap 6 / Gap 15]

        // ── Health Checks ─────────────────────────────────────────────────────
        var healthChecksBuilder = services.AddHealthChecks()
            .AddNpgSql(
                configuration.GetConnectionString("DefaultConnection")!,
                name: "postgres",
                tags: ["db", "ready"])
            .AddCheck<HikvisionDeviceHealthCheck>(
                "hikvision-devices",
                tags: ["device", "ready"]);

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            healthChecksBuilder.AddRedis(
                redisConnectionString,
                name: "redis",
                tags: ["cache", "ready"]);
        }

        return services;
    }
}
