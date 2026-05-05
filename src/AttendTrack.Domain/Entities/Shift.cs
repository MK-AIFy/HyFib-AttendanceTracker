using AttendTrack.Domain.Common;

namespace AttendTrack.Domain.Entities;

public sealed class Shift : AggregateRoot<Guid>
{
    /// <summary>Shift start time in IST.</summary>
    public string   Name                    { get; private set; } = default!;
    public TimeOnly StartTime               { get; private set; }
    public TimeOnly EndTime                 { get; private set; }
    public int      GracePeriodMinutes      { get; private set; }
    public int      OvertimeThresholdMinutes { get; private set; }
    /// <summary>True when EndTime < StartTime (crosses midnight).</summary>
    public bool     IsNightShift            { get; private set; }
    public bool     IsActive                { get; private set; }
    public DateTime CreatedAt               { get; private set; }

    private Shift() { }

    public static Shift Create(
        Guid id,
        string name,
        TimeOnly startTime,
        TimeOnly endTime,
        int gracePeriodMinutes       = 15,
        int overtimeThresholdMinutes = 30)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Shift
        {
            Id                       = id,
            Name                     = name,
            StartTime                = startTime,
            EndTime                  = endTime,
            GracePeriodMinutes       = gracePeriodMinutes,
            OvertimeThresholdMinutes = overtimeThresholdMinutes,
            IsNightShift             = endTime < startTime,
            IsActive                 = true,
            CreatedAt                = DateTime.UtcNow
        };
    }

    public void Deactivate() => IsActive = false;
}
