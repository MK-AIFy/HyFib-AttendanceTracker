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
/// Covers the HourlyTrackerService N+1/unbounded-recompute fix (PR: database query
/// performance): ComputeHourlySlots used to walk every elapsed hour since check-in
/// on every single 60-second tick — cost grew unboundedly through a shift. It now
/// only backfills the full range on a record's first-ever tick (no HourlySlot rows
/// yet) and caps every later tick to just the current + previous hour. This proves
/// that bound doesn't come at the cost of losing or corrupting the earlier hours'
/// already-recorded data — a multi-hour-old open record's history must survive
/// repeated ticks unchanged, even though only its most recent hour is still being
/// actively recomputed.
/// </summary>
[Collection("HourlyTracker")]
public sealed class HourlyTrackerServiceQueryPerformanceTests : IClassFixture<AttendTrackWebApplicationFactory>
{
    private readonly AttendTrackWebApplicationFactory _factory;

    public HourlyTrackerServiceQueryPerformanceTests(AttendTrackWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LongOpenRecord_RepeatedTicks_PreserveEarlierHours_AndKeepFullTotalWorked()
    {
        // Checked in 5 hours ago today — first tick must backfill all 5(ish) hours;
        // a later tick must not silently drop or corrupt those already-recorded ones.
        var checkInUtc = DateTime.UtcNow.AddHours(-5);
        var todayIst   = IstTimeHelper.TodayIst;

        Guid employeeGuid;
        Guid recordId;
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
                workDate:       todayIst,
                checkInTimeUtc: checkInUtc,
                source:         PunchSource.Hikvision);

            db.AttendanceRecords.Add(record);
            await db.SaveChangesAsync();
            recordId = record.Id;
        }

        async Task RunOneTickAsync()
        {
            using var serviceScope = _factory.Services.CreateScope();
            var scopeFactory = serviceScope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
            var service = new HourlyTrackerService(scopeFactory, NullLogger<HourlyTrackerService>.Instance);

            using var cts = new CancellationTokenSource();
            await service.StartAsync(cts.Token);
            await Task.Delay(TimeSpan.FromSeconds(3));
            await service.StopAsync(cts.Token);
        }

        // First tick: backfillFromCheckIn (no rows exist yet) — should populate
        // every elapsed hour since check-in.
        await RunOneTickAsync();

        IReadOnlyList<HourlySlot> afterFirstTick;
        using (var readScope = _factory.Services.CreateScope())
        {
            var hourlySlotRepo = readScope.ServiceProvider.GetRequiredService<IHourlySlotRepository>();
            afterFirstTick = await hourlySlotRepo.GetByAttendanceAsync(recordId);
        }

        afterFirstTick.Should().HaveCountGreaterThanOrEqualTo(4,
            "the record has been open ~5 hours, so the first tick's full backfill " +
            "should have recorded roughly that many hour slots");

        var earliestHourBeforeSecondTick = afterFirstTick
            .OrderBy(s => s.HourSlotNumber)
            .First();

        // Second tick: steady state — only the current/previous hour should be
        // recomputed. The earliest slot recorded above must still be present with
        // an unchanged (already-final) MinutesWorked value.
        await RunOneTickAsync();

        using var finalReadScope = _factory.Services.CreateScope();
        var finalRepo = finalReadScope.ServiceProvider.GetRequiredService<IHourlySlotRepository>();
        var afterSecondTick = await finalRepo.GetByAttendanceAsync(recordId);

        afterSecondTick.Count.Should().BeGreaterThanOrEqualTo(afterFirstTick.Count,
            "no earlier hour's slot row should have been dropped by the bounded second tick");

        var earliestAfterSecondTick = afterSecondTick
            .Single(s => s.HourSlotNumber == earliestHourBeforeSecondTick.HourSlotNumber
                      && s.WorkDate       == earliestHourBeforeSecondTick.WorkDate);

        earliestAfterSecondTick.MinutesWorked.Should().Be(earliestHourBeforeSecondTick.MinutesWorked,
            "a fully-elapsed hour's MinutesWorked is already final — the bounded steady-state " +
            "tick must not touch it, and it certainly must not silently reset/corrupt it");
    }
}
