namespace AttendTrack.Domain.ValueObjects;

/// <summary>Represents a worked duration with helpers for overtime detection.</summary>
public sealed record WorkDuration(TimeSpan Value)
{
    public static WorkDuration Zero => new(TimeSpan.Zero);

    public static WorkDuration From(TimeSpan value) => new(value);

    public double TotalHours    => Value.TotalHours;
    public int    TotalMinutes  => (int)Value.TotalMinutes;

    public bool IsOvertime(TimeSpan standardDuration) => Value > standardDuration;

    public WorkDuration Add(WorkDuration other) => new(Value + other.Value);

    public override string ToString() => $"{(int)Value.TotalHours:D2}h {Value.Minutes:D2}m";
}
