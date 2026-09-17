using AttendTrack.Domain.Entities;
using AttendTrack.Domain.Enums;
using AttendTrack.Domain.Interfaces.Repositories;
using AttendTrack.Infrastructure.Persistence;
using AttendTrack.Infrastructure.Security;
using AttendTrack.Infrastructure.Services;
using AttendTrack.Integration.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AttendTrack.Integration.Tests;

/// <summary>
/// HourlyTrackerService.ProcessOpenRecordsAsync queries
/// `db.AttendanceRecords.Where(r => r.WorkDate == todayIst && ...)`. An open night-shift
/// record's WorkDate is fixed at check-in time (yesterday, for a shift that starts before
/// midnight), but `todayIst` is today — so once midnight IST passes, that filter stops
/// matching the still-open record and the service silently stops recording HourlySlot
/// rows for the rest of the shift. This test seeds exactly that scenario against a real
/// Postgres instance and runs the actual hosted service (not a reimplementation of its
/// query) to prove the gap, matching the "HourlyTrackerService night-shift WorkDate
/// bookkeeping" item flagged as deferred in project docs.
/// </summary>
[Collection("HourlyTracker")]
public sealed class HourlyTrackerServiceTests : IClassFixture<AttendTrackWebApplicationFactory>
{
    private readonly AttendTrackWebApplicationFactory _factory;

    public HourlyTrackerServiceTests(AttendTrackWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenNightShiftRecord_GetsHourlySlotsRecorded_ForHoursAfterMidnight()
    {
        var yesterdayIst = IstTimeHelper.TodayIst.AddDays(-1);
        var checkInIst   = new DateTime(
            yesterdayIst.Year, yesterdayIst.Month, yesterdayIst.Day,
            22, 0, 0, DateTimeKind.Unspecified);
        var checkInUtc   = IstTimeHelper.ToUtc(checkInIst);

        Guid employeeGuid;
        using (var seedScope = _factory.Services.CreateScope())
        {
            var db    = seedScope.ServiceProvider.GetRequiredService<AttendTrackDbContext>();
            var emp   = await db.Employees.SingleAsync(e =>
                e.EmployeeCode == AttendTrackWebApplicationFactory.TestEmployeeCode);
            var shift = await db.Shifts.SingleAsync(s =>
                s.Id == AttendTrackWebApplicationFactory.TestShiftId);
            employeeGuid = emp.Id.Value;

            var record = AttendanceRecord.CheckIn(
                employeeId:     emp.Id,
                shift:          shift,
                workDate:       yesterdayIst,
                checkInTimeUtc: checkInUtc,
                source:         PunchSource.Hikvision);

            db.AttendanceRecords.Add(record);
            await db.SaveChangesAsync();
        }

        // Run the actual hosted service (not a copy of its query) for one pass.
        using (var serviceScope = _factory.Services.CreateScope())
        {
            var scopeFactory = serviceScope.ServiceProvider
                .GetRequiredService<IServiceScopeFactory>();
            var service = new HourlyTrackerService(
                scopeFactory, NullLogger<HourlyTrackerService>.Instance);

            using var cts = new CancellationTokenSource();
            await service.StartAsync(cts.Token);
            await Task.Delay(TimeSpan.FromSeconds(3)); // let the first immediate pass complete
            await service.StopAsync(cts.Token);
        }

        using var readScope = _factory.Services.CreateScope();
        var hourlySlotRepo = readScope.ServiceProvider.GetRequiredService<IHourlySlotRepository>();

        var todaySlots = await hourlySlotRepo.GetByDateAsync(IstTimeHelper.TodayIst);

        todaySlots.Where(s => s.EmployeeId == employeeGuid).Should().NotBeEmpty(
            "the night shift is still open past midnight IST, so today's hours (00:00 onward) " +
            "should have HourlySlot rows even though the record's WorkDate is still yesterday");
    }
}
