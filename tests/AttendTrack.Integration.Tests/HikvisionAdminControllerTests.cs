using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AttendTrack.Application.Interfaces;
using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.ValueObjects;
using AttendTrack.Infrastructure.Persistence;
using AttendTrack.Integration.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AttendTrack.Integration.Tests;

/// <summary>
/// GET /api/admin/hikvision/devices/{id} used to serialize the raw HikvisionDevice entity,
/// including AdminPasswordHash (bcrypt) and AdminPasswordProtected (a reversibly-encrypted
/// copy of the device's real admin password used by HikvisionIsapiService for outbound ISAPI
/// calls). The sibling list endpoint (GetDevices) already redacted these; the single-device
/// endpoint didn't. This test proves neither field reaches an HTTP client.
/// </summary>
[Collection("HikvisionAdmin")]
public sealed class HikvisionAdminControllerTests : IClassFixture<AttendTrackWebApplicationFactory>
{
    private readonly AttendTrackWebApplicationFactory _factory;

    public HikvisionAdminControllerTests(AttendTrackWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDevice_DoesNotExposeAdminPasswordHashOrProtectedPassword()
    {
        Guid deviceId;
        string knownHash;
        Employee admin;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AttendTrackDbContext>();

            var device = await db.HikvisionDevices
                .SingleAsync(d => d.SerialNumber == AttendTrackWebApplicationFactory.TestDeviceSerial);
            deviceId = device.Id;
            knownHash = device.AdminPasswordHash; // the actual secret this endpoint must never return

            // The seeded TestEmployeeCode is role=Employee — mint a real admin to authenticate as.
            var dept = await db.Departments.FirstAsync();
            var pinHash = BCrypt.Net.BCrypt.HashPassword("999999", workFactor: 4);
            admin = Employee.Create(
                id:             EmployeeId.New(),
                employeeCode:   "EMP-ADMIN-TEST",
                fullName:       "Test Admin",
                email:          "admin-test@test.com",
                phone:          "0000000000",
                kioskPin:       PinHash.From(pinHash),
                departmentId:   dept.Id,
                defaultShiftId: AttendTrackWebApplicationFactory.TestShiftId,
                role:           UserRole.SuperAdmin,
                joinedAt:       new DateOnly(2024, 1, 1));
            db.Employees.Add(admin);
            await db.SaveChangesAsync();
        }

        using var jwtScope = _factory.Services.CreateScope();
        var jwtService = jwtScope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var (token, _) = jwtService.Issue(admin);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/admin/hikvision/devices/{deviceId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();

        // Strongest check: the actual secret value must not appear anywhere in the payload.
        body.Should().NotContain(knownHash);

        // Also verify by property name (case-insensitively — ASP.NET Core's default
        // serializer camelCases keys), in case the hash's own characters happened to
        // collide with surrounding JSON text.
        using var doc = JsonDocument.Parse(body);
        var propertyNames = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        propertyNames.Should().NotContain(n => n.Equals("AdminPasswordHash", StringComparison.OrdinalIgnoreCase));
        propertyNames.Should().NotContain(n => n.Equals("AdminPasswordProtected", StringComparison.OrdinalIgnoreCase));
        propertyNames.Should().NotContain(n => n.Equals("AdminUsername", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetDevice_JwtAuthenticatedNonAdmin_Returns403()
    {
        // Regression guard for the fix above: adding AuthenticationSchemes to
        // [Authorize(Roles = ...)] must not turn it into "any authenticated user" —
        // a JWT-authenticated plain Employee should still be rejected by the role check.
        Guid deviceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AttendTrackDbContext>();
            var device = await db.HikvisionDevices
                .SingleAsync(d => d.SerialNumber == AttendTrackWebApplicationFactory.TestDeviceSerial);
            deviceId = device.Id;
        }

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AttendTrackDbContext>();
        var employee = await db2.Employees
            .SingleAsync(e => e.EmployeeCode == AttendTrackWebApplicationFactory.TestEmployeeCode);

        var jwtService = scope2.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var (token, _) = jwtService.Issue(employee); // role = Employee, seeded by SeedAsync

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/admin/hikvision/devices/{deviceId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
