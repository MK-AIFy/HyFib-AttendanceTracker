namespace AttendTrack.Infrastructure.Services;

/// <summary>
/// Shared XML-escaping for outbound Hikvision ISAPI payloads. Previously duplicated
/// privately in HikvisionIsapiService; HikvisionPollingService.BuildRawXmlPlaceholder
/// interpolated the same kind of device-sourced strings (employee name, card number)
/// with no escaping at all, so a name containing '&amp;'/'&lt;'/'&gt;' produced invalid XML
/// that HikvisionEventParser then silently discarded — dropping that employee's polled
/// events. Extracted so both call sites share one implementation.
/// </summary>
internal static class HikvisionXml
{
    /// <summary>
    /// Escapes user-supplied strings so they cannot inject XML structure into
    /// outbound payloads. Cheap pass — only the five XML special characters.
    /// </summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return System.Security.SecurityElement.Escape(value) ?? string.Empty;
    }
}
