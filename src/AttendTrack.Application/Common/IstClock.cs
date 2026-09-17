namespace AttendTrack.Application.Common;

/// <summary>
/// IST timezone helper for Application layer.
/// Mirrors Infrastructure.Security.IstTimeHelper without the Infrastructure dependency.
/// </summary>
internal static class IstClock
{
    private static readonly TimeZoneInfo IstZone = GetIstZone();

    public static DateTime NowIst  => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone);
    public static DateOnly TodayIst => DateOnly.FromDateTime(NowIst);

    public static DateTime ToIst(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(utc, IstZone);

    /// <summary>Formats as "15 Jan 2025 09:03 AM" IST.</summary>
    public static string FormatIst(DateTime utc)
        => ToIst(utc).ToString("dd MMM yyyy hh:mm tt");

    /// <summary>Formats as "09:03 AM" IST.</summary>
    public static string FormatIstTime(DateTime utc)
        => ToIst(utc).ToString("hh:mm tt");

    /// <summary>Formats as "15 Jan 2025" IST.</summary>
    public static string FormatIstDate(DateTime utc)
        => ToIst(utc).ToString("dd MMM yyyy");

    /// <summary>
    /// Formats a value that is already an IST wall-clock reading (e.g. Hikvision's
    /// DeviceLocalTime) as "15 Jan 2025 09:03 AM". Unlike FormatIst, this does NOT
    /// convert from UTC first — doing so would double-shift an already-IST value.
    /// </summary>
    public static string FormatAlreadyIst(DateTime ist)
        => ist.ToString("dd MMM yyyy hh:mm tt");

    private static TimeZoneInfo GetIstZone()
    {
        // IANA on macOS/Linux; Windows fallback
        if (TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Kolkata", out var tz)) return tz;
        return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
    }
}
