namespace AttendTrack.Domain.ValueObjects;

/// <summary>Represents a time window (start/end) within a single day.</summary>
public sealed record TimeSlot(TimeOnly Start, TimeOnly End)
{
    /// <summary>Handles overnight slots (e.g. 22:00–06:00).</summary>
    public TimeSpan Duration => End >= Start
        ? End - Start
        : TimeSpan.FromHours(24) - (Start - End);

    public bool Contains(TimeOnly time)
    {
        if (End >= Start) return time >= Start && time <= End;
        return time >= Start || time <= End; // overnight
    }

    public static TimeSlot From(TimeOnly start, TimeOnly end) => new(start, end);
}
