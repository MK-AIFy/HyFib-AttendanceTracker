using AttendTrack.Application.Queries.Reports;
using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace AttendTrack.Application.Tests;

/// <summary>
/// GetWeeklyReportHandler/GetMonthlyReportHandler computed TotalAbsent as
/// `allEmployees.Count - dtos.Count`, where allEmployees is a single snapshot of
/// *currently* active employees applied uniformly to every day in the report range.
/// That falsely counts an employee as absent on days before they even joined —
/// this test proves it by seeding one long-tenured employee and one hired mid-week,
/// with zero attendance records for anyone, and checking TotalAbsent per day.
/// </summary>
public sealed class ReportAbsenceCountTests
{
    [Fact]
    public async Task WeeklyReport_DoesNotCountEmployee_AsAbsent_BeforeTheyJoined()
    {
        var weekStart = new DateOnly(2026, 3, 2);   // Monday
        var joinDay   = weekStart.AddDays(3);        // Thursday — mid-week hire

        var veteran = Employee.Create(
            id: EmployeeId.New(), employeeCode: "EMP-VET", fullName: "Veteran",
            email: "vet@test.com", phone: "1", kioskPin: PinHash.From("hash"),
            departmentId: Guid.NewGuid(), defaultShiftId: Guid.NewGuid(),
            role: UserRole.Employee, joinedAt: weekStart.AddYears(-1));

        var newHire = Employee.Create(
            id: EmployeeId.New(), employeeCode: "EMP-NEW", fullName: "New Hire",
            email: "new@test.com", phone: "2", kioskPin: PinHash.From("hash"),
            departmentId: Guid.NewGuid(), defaultShiftId: Guid.NewGuid(),
            role: UserRole.Employee, joinedAt: joinDay);

        var empRepo = new Mock<IEmployeeRepository>();
        empRepo.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { veteran, newHire });

        var attRepo = new Mock<IAttendanceRepository>();
        attRepo.Setup(r => r.GetByDateRangeAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AttendanceRecord>()); // nobody has a record — everyone eligible is "absent"

        var handler = new GetWeeklyReportHandler(attRepo.Object, empRepo.Object);
        var report  = await handler.Handle(new GetWeeklyReportQuery(weekStart), CancellationToken.None);

        var dayBeforeJoin = report.Single(d => d.Date == weekStart.AddDays(2).ToString("dd MMM yyyy"));
        var dayOfJoin     = report.Single(d => d.Date == joinDay.ToString("dd MMM yyyy"));

        dayBeforeJoin.TotalAbsent.Should().Be(1,
            "only the veteran employee had joined yet — the new hire didn't exist that day");
        dayOfJoin.TotalAbsent.Should().Be(2,
            "both employees are now eligible and both are absent (no records)");
    }
}
