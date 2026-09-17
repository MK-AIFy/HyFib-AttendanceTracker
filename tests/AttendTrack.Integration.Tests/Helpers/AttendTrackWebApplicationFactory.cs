using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.ValueObjects;
using AttendTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace AttendTrack.Integration.Tests.Helpers;

/// <summary>
/// WebApplicationFactory that spins up a real PostgreSQL container (TestContainers) and
/// boots the full ASP.NET Core pipeline.
///
/// Key design decisions:
/// - Connection string override via ConfigureAppConfiguration → AddInfrastructure reads it automatically.
/// - All IHostedService background jobs are removed to keep tests fast and deterministic.
/// - TestRemoteIpStartupFilter injects a middleware that reads the X-Test-RemoteIp header
///   and sets context.Connection.RemoteIpAddress, enabling IP-based auth tests.
/// - BCrypt work factor = 4 (minimum) for fast test startup.
/// </summary>
public sealed class AttendTrackWebApplicationFactory
    : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>,
      IAsyncLifetime
{
    // ── Test constants ────────────────────────────────────────────────────────

    public const string TestDeviceIp       = "192.168.1.50";
    public const string TestDeviceSerial   = "TESTSERIAL001";
    public const string TestDeviceUsername = "admin";
    public const string TestDevicePassword = "hiktest123";
    public const string TestEmployeeCode   = "EMP-001";

    public static readonly Guid TestEmployeeId  = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid TestShiftId     = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid TestSecondShiftId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // ── TestContainers ────────────────────────────────────────────────────────

#pragma warning disable CS0618   // PostgreSqlBuilder() parameterless constructor — image default is fine for tests
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("attendtrack_test")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .Build();
#pragma warning restore CS0618

    // ── Constructor ───────────────────────────────────────────────────────────

    // Program.cs reads Jwt:SecretKey (and AddInfrastructure reads the connection
    // strings) directly off `builder.Configuration` *before* `builder.Build()` runs.
    // WebApplicationFactory's ConfigureAppConfiguration overrides are only merged in
    // when the deferred test host builder runs as part of that Build() call, so they
    // arrive too late for that early code and are silently ignored. Environment
    // variables, however, are picked up immediately by WebApplication.CreateBuilder's
    // own AddEnvironmentVariables() source, so they're set here instead.
    public AttendTrackWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__SecretKey", "integration-test-secret-key-min32chars!!");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "AttendTrack");
        Environment.SetEnvironmentVariable("Jwt__Audience", "AttendTrackUsers");
        Environment.SetEnvironmentVariable("Hikvision__FaceCapturePath", Path.GetTempPath());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", ""); // Disables Redis → uses IMemoryCache
    }

    // ── IAsyncLifetime ────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Must be set before the first Services access below, which triggers host build.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());

        // Trigger host build + apply real migrations + seed data.
        // NOTE: EnsureCreatedAsync() is NOT sufficient here — the Testcontainers Postgres
        // image already creates the (empty) database as part of container startup, so EF
        // sees "database exists" and skips schema creation entirely, leaving zero tables.
        // Applying the actual migrations also exercises the real deployment path.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AttendTrackDbContext>();
        await db.Database.MigrateAsync();
        await SeedAsync(db);
    }

    public new async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        await base.DisposeAsync();
    }

    // ── WebApplicationFactory override ────────────────────────────────────────

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove ALL background services (polling, hourly tracker, missed-punch, etc.)
            var hostedServiceDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var d in hostedServiceDescriptors)
                services.Remove(d);

            // Inject IP-spoofing middleware so we can test IP-based auth
            services.AddSingleton<IStartupFilter, TestRemoteIpStartupFilter>();
        });
    }

    // ── Helper: create a client that appears to come from the device IP ────────

    /// <summary>
    /// Creates an <see cref="HttpClient"/> pre-configured with:
    /// - X-Test-RemoteIp = TestDeviceIp (so the webhook auth middleware sees the device IP)
    /// - Authorization: Basic <TestDeviceUsername>:<TestDevicePassword>
    /// </summary>
    public HttpClient CreateDeviceClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-RemoteIp", TestDeviceIp);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", BasicAuthToken());
        return client;
    }

    /// <summary>Returns a Base64-encoded "username:password" for HTTP Basic auth.</summary>
    public static string BasicAuthToken() =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{TestDeviceUsername}:{TestDevicePassword}"));

    // ── Helper: query event log count ─────────────────────────────────────────

    public async Task<int> CountEventLogsAsync(string? employeeCode = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AttendTrackDbContext>();
        var query = db.HikvisionEventLogs.AsQueryable();
        if (employeeCode is not null)
            query = query.Where(e => e.EmployeeCode == employeeCode);
        return await query.CountAsync();
    }

    // ── Seed data ─────────────────────────────────────────────────────────────

    private static async Task SeedAsync(AttendTrackDbContext db)
    {
        var deptId  = Guid.NewGuid();
        var shiftId = TestShiftId;
        var empId   = EmployeeId.From(TestEmployeeId);

        var dept = Department.Create(deptId, "Engineering");
        var shift = Shift.Create(shiftId, "Morning",
            new TimeOnly(9, 0), new TimeOnly(18, 0), gracePeriodMinutes: 15);
        var secondShift = Shift.Create(TestSecondShiftId, "Evening",
            new TimeOnly(14, 0), new TimeOnly(23, 0), gracePeriodMinutes: 15);

        // Use a cheap BCrypt work factor so test startup is fast
        var pinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 4);
        var employee = Employee.Create(
            id:             empId,
            employeeCode:   TestEmployeeCode,
            fullName:       "John Doe",
            email:          "john.doe@test.com",
            phone:          "9876543210",
            kioskPin:       PinHash.From(pinHash),
            departmentId:   deptId,
            defaultShiftId: shiftId,
            role:           UserRole.Employee,
            joinedAt:       new DateOnly(2024, 1, 1));

        var devicePasswordHash = BCrypt.Net.BCrypt.HashPassword(
            TestDevicePassword, workFactor: 4);

        var device = HikvisionDevice.Register(
            id:                    Guid.NewGuid(),
            deviceName:            "Test Access Controller",
            model:                 "DS-K1T320MFWX",
            serialNumber:          TestDeviceSerial,
            ipAddress:             TestDeviceIp,
            port:                  80,
            adminUsername:         TestDeviceUsername,
            adminPasswordHash:     devicePasswordHash,
            adminPasswordProtected: null,
            location:              "Test Lab",
            firmwareVersion:       "V3.5.2 build 240701");

        db.Departments.Add(dept);
        db.Shifts.Add(shift);
        db.Shifts.Add(secondShift);
        db.Employees.Add(employee);
        db.HikvisionDevices.Add(device);
        await db.SaveChangesAsync();
    }
}

/// <summary>
/// Injects a middleware at the VERY START of the pipeline that reads X-Test-RemoteIp
/// and sets <c>context.Connection.RemoteIpAddress</c> accordingly.
/// This is the only safe way to spoof the TCP-level IP in <see cref="WebApplicationFactory"/>
/// without touching the production middleware.
/// </summary>
internal sealed class TestRemoteIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-RemoteIp", out var ipValue)
                    && IPAddress.TryParse(ipValue, out var ip))
                {
                    context.Connection.RemoteIpAddress = ip;
                }
                await nextMiddleware(context);
            });
            next(app);
        };
}
