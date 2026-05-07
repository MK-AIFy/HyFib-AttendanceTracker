using AttendTrack.Domain.Entities;
using AttendTrack.Infrastructure.Services;
using FluentAssertions;

namespace AttendTrack.Application.Tests;

/// <summary>
/// Tests for the pure function <see cref="HourlyTrackerService.ComputeHourlySlots"/>.
/// Drives the function with explicit UTC timestamps for the attendance window and break
/// windows; the function converts to IST internally via <c>IstTimeHelper</c> (UTC + 5:30)
/// before bucketing into hour slots.
/// </summary>
public sealed class HourlyTrackerServiceTests
{
    private static readonly Guid AttendanceId = Guid.NewGuid();
    private static readonly Guid EmployeeId   = Guid.NewGuid();

    /// <summary>IST = UTC + 5:30. Convert an IST wall-clock time to UTC for test inputs.</summary>
    private static DateTime IstToUtc(int hour, int minute, int second = 0)
    {
        var istOffset = TimeSpan.FromHours(5.5);
        var ist       = new DateTimeOffset(2025, 1, 15, hour, minute, second, istOffset);
        return ist.UtcDateTime;
    }

    private static List<HourlySlot> Run(
        DateTime checkInUtc,
        DateTime? checkOutUtc,
        IReadOnlyList<(DateTime StartUtc, DateTime? EndUtc)>? breaks = null,
        DateTime? nowUtc = null)
        => HourlyTrackerService.ComputeHourlySlots(
            attendanceRecordId: AttendanceId,
            employeeId:         EmployeeId,
            checkInUtc:         checkInUtc,
            checkOutUtc:        checkOutUtc,
            breaks:             breaks ?? [],
            nowUtc:             nowUtc ?? DateTime.UtcNow);

    // ── Test 1: Full-hour worked, no breaks ──────────────────────────────────────

    [Fact]
    public void FullHourWorked_NoBreaks_Yields60Minutes()
    {
        var checkIn  = IstToUtc(9, 0);
        var checkOut = IstToUtc(10, 0);

        var slots = Run(checkIn, checkOut);

        slots.Should().HaveCount(1);
        slots[0].HourSlotNumber.Should().Be(9);
        slots[0].MinutesWorked.Should().Be(60);
        slots[0].IsBreak.Should().BeFalse();
    }

    // ── Test 2: Mid-hour check-in ───────────────────────────────────────────────

    [Fact]
    public void MidHourCheckIn_AccountsForPartialFirstHour()
    {
        // Check in at 09:15 IST, out at 10:30 IST → slot[9]=45 min, slot[10]=30 min.
        var checkIn  = IstToUtc(9, 15);
        var checkOut = IstToUtc(10, 30);

        var slots = Run(checkIn, checkOut);

        slots.Should().HaveCount(2);
        slots.Single(s => s.HourSlotNumber == 9).MinutesWorked.Should().Be(45);
        slots.Single(s => s.HourSlotNumber == 10).MinutesWorked.Should().Be(30);
        slots.Should().OnlyContain(s => !s.IsBreak);
    }

    // ── Test 3: Mid-hour check-out ──────────────────────────────────────────────

    [Fact]
    public void MidHourCheckOut_AccountsForPartialFinalHour()
    {
        // 09:00–17:30 IST → slot[9..16] = 60, slot[17] = 30.
        var checkIn  = IstToUtc(9, 0);
        var checkOut = IstToUtc(17, 30);

        var slots = Run(checkIn, checkOut);

        slots.Should().HaveCount(9);
        for (var h = 9; h <= 16; h++)
            slots.Single(s => s.HourSlotNumber == h).MinutesWorked.Should().Be(60);
        slots.Single(s => s.HourSlotNumber == 17).MinutesWorked.Should().Be(30);
    }

    // ── Test 4: Break entirely within one hour ──────────────────────────────────

    [Fact]
    public void BreakEntirelyWithinOneHour_SubtractsMinutes_AndFlagsAsBreak()
    {
        // 09:00–10:00 work; break 09:15–09:45 → slot[9] = 60-30 = 30 min.
        // breakMinutes (30) * 2 >= rawMinutes (60) — so IsBreak = true.
        var checkIn  = IstToUtc(9, 0);
        var checkOut = IstToUtc(10, 0);
        var breaks   = new[] { (IstToUtc(9, 15), (DateTime?)IstToUtc(9, 45)) };

        var slots = Run(checkIn, checkOut, breaks);

        slots.Should().HaveCount(1);
        slots[0].HourSlotNumber.Should().Be(9);
        slots[0].MinutesWorked.Should().Be(30);
        slots[0].IsBreak.Should().BeTrue();
    }

    // ── Test 5: Short break inside an hour does NOT flag IsBreak ────────────────

    [Fact]
    public void ShortBreakInsideHour_SubtractsMinutes_DoesNotFlagAsBreak()
    {
        // 12:00–13:00 work; break 12:10–12:25 (15 min) → slot[12] = 60-15 = 45 min.
        // breakMinutes (15) * 2 = 30, rawMinutes = 60 → not flagged.
        var checkIn  = IstToUtc(12, 0);
        var checkOut = IstToUtc(13, 0);
        var breaks   = new[] { (IstToUtc(12, 10), (DateTime?)IstToUtc(12, 25)) };

        var slots = Run(checkIn, checkOut, breaks);

        slots.Single(s => s.HourSlotNumber == 12).MinutesWorked.Should().Be(45);
        slots.Single(s => s.HourSlotNumber == 12).IsBreak.Should().BeFalse();
    }

    // ── Test 6: Break crosses an hour boundary ──────────────────────────────────

    [Fact]
    public void BreakCrossingHourBoundary_SubtractsFromBothHours()
    {
        // 12:00–14:00 work; break 12:45–13:15 → slot[12] = 60-15 = 45, slot[13] = 60-15 = 45.
        var checkIn  = IstToUtc(12, 0);
        var checkOut = IstToUtc(14, 0);
        var breaks   = new[] { (IstToUtc(12, 45), (DateTime?)IstToUtc(13, 15)) };

        var slots = Run(checkIn, checkOut, breaks);

        slots.Single(s => s.HourSlotNumber == 12).MinutesWorked.Should().Be(45);
        slots.Single(s => s.HourSlotNumber == 13).MinutesWorked.Should().Be(45);
        slots.Should().OnlyContain(s => !s.IsBreak);
    }

    // ── Test 7: Open record (no checkout) uses nowUtc ───────────────────────────

    [Fact]
    public void OpenRecord_UsesNowUtcForOpenWindow()
    {
        // Check in at 09:00 IST, no checkout, "now" = 11:30 IST.
        var checkIn = IstToUtc(9, 0);
        var nowUtc  = IstToUtc(11, 30);

        var slots = Run(checkIn, checkOutUtc: null, nowUtc: nowUtc);

        slots.Should().HaveCount(3);
        slots.Single(s => s.HourSlotNumber == 9).MinutesWorked.Should().Be(60);
        slots.Single(s => s.HourSlotNumber == 10).MinutesWorked.Should().Be(60);
        slots.Single(s => s.HourSlotNumber == 11).MinutesWorked.Should().Be(30);
    }

    // ── Test 8: Active break on open record clamps to nowUtc ────────────────────

    [Fact]
    public void OpenBreak_OnOpenRecord_ClampsToNowUtc()
    {
        // 09:00–<now=09:40>, active break 09:20–<open> → slot[9] = 40-20 = 20 min.
        var checkIn = IstToUtc(9, 0);
        var nowUtc  = IstToUtc(9, 40);
        var breaks  = new[] { (IstToUtc(9, 20), (DateTime?)null) };

        var slots = Run(checkIn, checkOutUtc: null, breaks: breaks, nowUtc: nowUtc);

        slots.Should().HaveCount(1);
        slots[0].MinutesWorked.Should().Be(20);
    }
}
