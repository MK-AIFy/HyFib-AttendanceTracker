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
/// attendance_records.employee_id had no foreign key to employees at all.
/// DataRetentionPurgeService.PurgeExpiredEmployeesAsync hard-deletes employees past the
/// 7-year DPDP retention window and its own doc comment claims this removes "all
/// FK-cascaded data" — but without an actual FK, deleting the employee left every
/// attendance_records/hourly_slots/BreakRecords row for them silently orphaned instead.
/// This test proves deleting an employee now actually cascades: it seeds a real
/// AttendanceRecord for an employee, deletes the employee directly via EF (the same
/// operation the purge service performs), and asserts the attendance record is gone too
/// rather than orphaned or blocking the delete with a FK violation.
/// </summary>
[Collection("AttendanceRecordForeignKey")]
public sealed class AttendanceRecordForeignKeyTests : IClassFixture<AttendTrackWebApplicationFactory>
{
    private readonly AttendTrackWebApplicationFactory _factory;

    public AttendanceRecordForeignKeyTests(AttendTrackWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeletingEmployee_CascadesToTheirAttendanceRecords()
    {
        Guid attendanceRecordId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AttendTrackDbContext>();

            var dept  = await db.Departments.FirstAsync();
            var shift = await db.Shifts.SingleAsync(s => s.Id == AttendTrackWebApplicationFactory.TestShiftId);

            var pinHash = BCrypt.Net.BCrypt.HashPassword("123456", workFactor: 4);
            var employee = Employee.Create(
                id:             EmployeeId.New(),
                employeeCode:   "EMP-PURGE-TEST",
                fullName:       "Purge Target",
                email:          "purge-target@test.com",
                phone:          "1111111111",
                kioskPin:       PinHash.From(pinHash),
                departmentId:   dept.Id,
                defaultShiftId: shift.Id,
                role:           UserRole.Employee,
                joinedAt:       new DateOnly(2020, 1, 1));
            db.Employees.Add(employee);
            await db.SaveChangesAsync();

            var record = AttendanceRecord.CheckIn(
                employeeId:     employee.Id,
                shift:          shift,
                workDate:       new DateOnly(2020, 1, 2),
                checkInTimeUtc: new DateTime(2020, 1, 2, 3, 30, 0, DateTimeKind.Utc),
                source:         PunchSource.Hikvision);
            attendanceRecordId = record.Id;
            db.AttendanceRecords.Add(record);
            await db.SaveChangesAsync();
        }

        using (var deleteScope = _factory.Services.CreateScope())
        {
            var db = deleteScope.ServiceProvider.GetRequiredService<AttendTrackDbContext>();
            var employee = await db.Employees.IgnoreQueryFilters()
                .SingleAsync(e => e.EmployeeCode == "EMP-PURGE-TEST");

            // Same operation DataRetentionPurgeService.PurgeExpiredEmployeesAsync performs.
            db.Employees.Remove(employee);
            var act = async () => await db.SaveChangesAsync();
            await act.Should().NotThrowAsync("the FK is Cascade, not Restrict — deleting the " +
                "employee must not be blocked by their own attendance history");
        }

        using var readScope = _factory.Services.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<AttendTrackDbContext>();

        var orphanedRecord = await readDb.AttendanceRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == attendanceRecordId);
        orphanedRecord.Should().BeNull(
            "the attendance record must be cascade-deleted with its employee, not orphaned");
    }
}
