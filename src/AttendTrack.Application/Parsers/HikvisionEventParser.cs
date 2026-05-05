using System.Xml.Linq;
using AttendTrack.Domain.ValueObjects;

namespace AttendTrack.Application.Parsers;

/// <summary>
/// Parses Hikvision DS-K1T320MFWX EventNotificationAlert XML payloads.
/// Handles both namespaced and non-namespaced XML (firmware version variations).
/// </summary>
public static class HikvisionEventParser
{
    // Static IST zone reused across all calls (thread-safe read-only)
    private static readonly TimeZoneInfo IstZone = GetIstZone();

    public static HikvisionEventParsed Parse(string xml)
    {
        var doc  = XDocument.Parse(xml);
        var ns   = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        var root = doc.Root;

        // AccessControllerEvent may be namespaced or not (firmware variations)
        var ace = root?.Element(ns + "AccessControllerEvent")
               ?? root?.Element("AccessControllerEvent");

        if (ace is null)
            throw new InvalidOperationException(
                "Payload missing AccessControllerEvent element. Raw XML may be malformed.");

        // --- Timestamp: prefer dateTime with timezone offset (IST+05:30) ---
        DateTime eventTimeUtc = DateTime.UtcNow; // fallback = server time
        var dateTimeStr = GetRootValue(root, ns, "dateTime");

        if (!string.IsNullOrWhiteSpace(dateTimeStr)
            && DateTimeOffset.TryParse(dateTimeStr, out var parsed))
        {
            eventTimeUtc = parsed.UtcDateTime;
        }
        else
        {
            // Fallback: Unix epoch UTC from <time> inside AccessControllerEvent
            var epochStr = GetValue(ace, ns, "time");
            if (!string.IsNullOrWhiteSpace(epochStr)
                && long.TryParse(epochStr, out long epoch))
            {
                eventTimeUtc = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
            }
        }

        // Device serial: prefer macAddress from root; some firmware puts it in ace
        var deviceSerial = GetRootValue(root, ns, "macAddress")
                        ?? GetValue(ace, ns, "serialNo")
                        ?? "UNKNOWN";

        return new HikvisionEventParsed(
            DeviceSerial:      deviceSerial,
            EmployeeNoString:  GetValue(ace, ns, "employeeNoString") ?? string.Empty,
            EmployeeName:      GetValue(ace, ns, "name")             ?? string.Empty,
            AttendanceStatus:  GetValue(ace, ns, "attendanceStatus") ?? string.Empty,
            CurrentVerifyMode: GetValue(ace, ns, "currentVerifyMode") ?? string.Empty,
            CardNo:            NullIfEmpty(GetValue(ace, ns, "cardNo")),
            EventTimeUtc:      eventTimeUtc,
            DeviceLocalTime:   TimeZoneInfo.ConvertTimeFromUtc(eventTimeUtc, IstZone));
    }

    private static string? GetValue(XElement parent, XNamespace ns, string name)
        => parent.Element(ns + name)?.Value
        ?? parent.Element(name)?.Value;

    private static string? GetRootValue(XElement? root, XNamespace ns, string name)
        => root?.Element(ns + name)?.Value
        ?? root?.Element(name)?.Value;

    private static string? NullIfEmpty(string? s)
        => string.IsNullOrEmpty(s) ? null : s;

    private static TimeZoneInfo GetIstZone()
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Kolkata", out var tz)) return tz;
        return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
    }
}
