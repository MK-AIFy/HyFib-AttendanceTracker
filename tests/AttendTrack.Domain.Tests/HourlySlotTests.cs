using AttendTrack.Domain.Entities;
using FluentAssertions;

namespace AttendTrack.Domain.Tests;

public sealed class HourlySlotTests
{
    private static readonly Guid _attendanceId = Guid.NewGuid();
    private static readonly Guid _employeeId   = Guid.NewGuid();
    private static readonly DateOnly _workDate = new(2025, 1, 15);

    // ── Test 1: Valid creation — boundary values ──────────────────────────────

    [Theory]
    [InlineData(0,  0,  false, false)]
    [InlineData(0,  60, false, false)]
    [InlineData(23, 0,  false, false)]
    [InlineData(23, 60, false, false)]
    [InlineData(12, 45, true,  false)]   // break slot
    [InlineData(20, 30, false, true)]    // overtime slot
    public void Create_ValidArguments_ReturnsHourlySlot(
        int  hourSlot, int minutesWorked, bool isBreak, bool isOvertime)
    {
        var slot = HourlySlot.Create(
            _attendanceId, _employeeId, _workDate,
            hourSlot, minutesWorked, isBreak, isOvertime);

        slot.Should().NotBeNull();
        slot.HourSlotNumber.Should().Be(hourSlot);
        slot.MinutesWorked.Should().Be(minutesWorked);
        slot.IsBreak.Should().Be(isBreak);
        slot.IsOvertime.Should().Be(isOvertime);
        slot.AttendanceRecordId.Should().Be(_attendanceId);
        slot.EmployeeId.Should().Be(_employeeId);
        slot.WorkDate.Should().Be(_workDate);
    }

    // ── Test 2: hour = 24 must throw ─────────────────────────────────────────

    [Fact]
    public void Create_HourSlot24_ThrowsArgumentOutOfRangeException()
    {
        var act = () => HourlySlot.Create(_attendanceId, _employeeId, _workDate,
            hourSlot: 24, minutesWorked: 0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("hourSlot");
    }

    // ── Test 3: negative hour must throw ─────────────────────────────────────

    [Fact]
    public void Create_NegativeHourSlot_ThrowsArgumentOutOfRangeException()
    {
        var act = () => HourlySlot.Create(_attendanceId, _employeeId, _workDate,
            hourSlot: -1, minutesWorked: 30);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("hourSlot");
    }

    // ── Test 4: minutes = 61 must throw ───────────────────────────────────────

    [Fact]
    public void Create_MinutesWorked61_ThrowsArgumentOutOfRangeException()
    {
        var act = () => HourlySlot.Create(_attendanceId, _employeeId, _workDate,
            hourSlot: 9, minutesWorked: 61);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("minutesWorked");
    }

    // ── Test 5: negative minutes must throw ───────────────────────────────────

    [Fact]
    public void Create_NegativeMinutes_ThrowsArgumentOutOfRangeException()
    {
        var act = () => HourlySlot.Create(_attendanceId, _employeeId, _workDate,
            hourSlot: 9, minutesWorked: -1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("minutesWorked");
    }

    // ── Test 6: Update changes values ────────────────────────────────────────

    [Fact]
    public void Update_ChangesMinutesWorkedAndFlags()
    {
        var slot = HourlySlot.Create(
            _attendanceId, _employeeId, _workDate, 9, 30);

        slot.Update(minutesWorked: 45, isBreak: false, isOvertime: true);

        slot.MinutesWorked.Should().Be(45);
        slot.IsOvertime.Should().BeTrue();
        slot.IsBreak.Should().BeFalse();
    }

    // ── Test 7: Update with invalid minutes throws ───────────────────────────

    [Fact]
    public void Update_InvalidMinutes_Throws()
    {
        var slot = HourlySlot.Create(
            _attendanceId, _employeeId, _workDate, 9, 30);

        var act = () => slot.Update(minutesWorked: 61);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("minutesWorked");
    }
}
