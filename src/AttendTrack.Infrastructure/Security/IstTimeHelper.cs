namespace AttendTrack.Infrastructure.Security;

/// <summary>
/// Centralised IST (Indian Standard Time, UTC+5:30) conversion utilities.
/// ALL display-facing times use these helpers. Stored times are always UTC TIMESTAMPTZ.
/// Gap 5: Single source of truth for timezone handling.
/// </summary>
public static class IstTimeHelper
{
    private static readonly TimeZoneInfo Ist = GetIstZone();

    public static string ToIst(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(utc, Ist).ToString("dd MMM yyyy hh:mm tt");

    public static DateTime ToIstDateTime(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(utc, Ist);

    public static DateTime ToUtc(DateTime ist)
        => TimeZoneInfo.ConvertTimeToUtc(
               DateTime.SpecifyKind(ist, DateTimeKind.Unspecified), Ist);

    public static DateTime NowIst
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Ist);

    public static DateOnly TodayIst
        => DateOnly.FromDateTime(NowIst);

    private static TimeZoneInfo GetIstZone()
    {
        // IANA id on macOS / Linux; Windows uses "India Standard Time"
        if (TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Kolkata", out var tz)) return tz;
        return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
    }
}
