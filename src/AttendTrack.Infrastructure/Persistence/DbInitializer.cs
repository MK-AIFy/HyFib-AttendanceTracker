using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.ValueObjects;
using AttendTrack.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AttendTrack.Infrastructure.Persistence;

/// <summary>
/// Seeds essential reference data (Departments, Shifts) on first run.
/// Safe to call on every startup — checks before inserting.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db     = scope.ServiceProvider.GetRequiredService<AttendTrackDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AttendTrackDbContext>>();

        try
        {
            // Run any pending migrations automatically
            await db.Database.MigrateAsync();

            await SeedDepartmentsAsync(db, logger);
            await SeedShiftsAsync(db, logger);
            await SeedAdminAsync(db, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DbInitializer failed — app will still start");
        }
    }

    // ── Departments ──────────────────────────────────────────────────────────

    private static async Task SeedDepartmentsAsync(AttendTrackDbContext db, ILogger logger)
    {
        if (await db.Departments.AnyAsync())
            return;

        var departments = new[]
        {
            Department.Create(Guid.NewGuid(), "General",       "Default department for all employees"),
            Department.Create(Guid.NewGuid(), "Engineering",   "Software & IT engineering"),
            Department.Create(Guid.NewGuid(), "HR",            "Human resources"),
            Department.Create(Guid.NewGuid(), "Operations",    "Operations & facilities"),
            Department.Create(Guid.NewGuid(), "Management",    "Senior management"),
        };

        db.Departments.AddRange(departments);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} departments", departments.Length);
    }

    // ── Shifts ───────────────────────────────────────────────────────────────

    private static async Task SeedShiftsAsync(AttendTrackDbContext db, ILogger logger)
    {
        if (await db.Shifts.AnyAsync())
            return;

        var shifts = new[]
        {
            Shift.Create(
                id:                    Guid.NewGuid(),
                name:                  "Standard (9 AM – 6 PM)",
                startTime:             new TimeOnly(9,  0),
                endTime:               new TimeOnly(18, 0),
                gracePeriodMinutes:    15,
                overtimeThresholdMinutes: 30),

            Shift.Create(
                id:                    Guid.NewGuid(),
                name:                  "Morning (8 AM – 5 PM)",
                startTime:             new TimeOnly(8,  0),
                endTime:               new TimeOnly(17, 0),
                gracePeriodMinutes:    15,
                overtimeThresholdMinutes: 30),

            Shift.Create(
                id:                    Guid.NewGuid(),
                name:                  "Evening (2 PM – 10 PM)",
                startTime:             new TimeOnly(14, 0),
                endTime:               new TimeOnly(22, 0),
                gracePeriodMinutes:    15,
                overtimeThresholdMinutes: 30),

            Shift.Create(
                id:                    Guid.NewGuid(),
                name:                  "Night (10 PM – 6 AM)",
                startTime:             new TimeOnly(22, 0),
                endTime:               new TimeOnly(6,  0),
                gracePeriodMinutes:    15,
                overtimeThresholdMinutes: 30),
        };

        db.Shifts.AddRange(shifts);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} shifts", shifts.Length);
    }

    // ── Default SuperAdmin ───────────────────────────────────────────────────

    private static async Task SeedAdminAsync(AttendTrackDbContext db, ILogger logger)
    {
        if (await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.Role == UserRole.SuperAdmin))
            return;

        var dept  = await db.Departments.OrderBy(d => d.Name).FirstAsync();
        var shift = await db.Shifts.OrderBy(s => s.Name).FirstAsync();

        var admin = Employee.Create(
            id:             EmployeeId.From(Guid.NewGuid()),
            employeeCode:   "ADMIN-001",
            fullName:       "System Administrator",
            email:          "admin@attendtrack.local",
            phone:          "0000000000",
            kioskPin:       PinHash.From(PinHasher.Hash("123456")),
            departmentId:   dept.Id,
            defaultShiftId: shift.Id,
            role:           UserRole.SuperAdmin,
            joinedAt:       DateOnly.FromDateTime(DateTime.UtcNow));

        db.Employees.Add(admin);
        await db.SaveChangesAsync();

        logger.LogWarning("⚠️  Default SuperAdmin created — code: ADMIN-001, PIN: 123456 — CHANGE IMMEDIATELY");
    }
}
