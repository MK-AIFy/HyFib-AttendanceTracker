using AttendTrack.Domain.Common;

namespace AttendTrack.Domain.Entities;

/// <summary>
/// Tracks minutes worked per IST hour (0-23) within an attendance record.
/// Enables the hourly heat-map UI and granular productivity reports (Gap 4).
/// </summary>
public sealed class HourlySlot : AggregateRoot<Guid>
{
    public Guid     AttendanceRecordId { get; private set; }
    public Guid     EmployeeId         { get; private set; }
    public DateOnly WorkDate           { get; private set; }
    /// <summary>IST hour number 0-23.</summary>
    public int      HourSlotNumber     { get; private set; }
    /// <summary>Minutes actively worked in this hour slot (0-60).</summary>
    public int      MinutesWorked      { get; private set; }
    public bool     IsBreak            { get; private set; }
    public bool     IsOvertime         { get; private set; }
    public DateTime CreatedAt          { get; private set; }
    public DateTime UpdatedAt          { get; private set; }

    private HourlySlot() { }

    public static HourlySlot Create(
        Guid attendanceRecordId,
        Guid employeeId,
        DateOnly workDate,
        int hourSlot,
        int minutesWorked,
        bool isBreak    = false,
        bool isOvertime = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(hourSlot,      0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hourSlot,   23);
        ArgumentOutOfRangeException.ThrowIfLessThan(minutesWorked,  0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minutesWorked, 60);

        return new HourlySlot
        {
            Id                 = Guid.NewGuid(),
            AttendanceRecordId = attendanceRecordId,
            EmployeeId         = employeeId,
            WorkDate           = workDate,
            HourSlotNumber     = hourSlot,
            MinutesWorked      = minutesWorked,
            IsBreak            = isBreak,
            IsOvertime         = isOvertime,
            CreatedAt          = DateTime.UtcNow,
            UpdatedAt          = DateTime.UtcNow
        };
    }

    public void Update(int minutesWorked, bool isBreak = false, bool isOvertime = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minutesWorked,  0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minutesWorked, 60);

        MinutesWorked = minutesWorked;
        IsBreak       = isBreak;
        IsOvertime    = isOvertime;
        UpdatedAt     = DateTime.UtcNow;
    }
}
