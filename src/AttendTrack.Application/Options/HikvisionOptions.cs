namespace AttendTrack.Application.Options;

/// <summary>
/// Controls how AttendTrack ingests events from Hikvision devices.
/// Fallback (default): polling only runs when the webhook has been silent
/// for <see cref="HikvisionOptions.WebhookGapMinutes"/>. Primary: polling
/// runs unconditionally on every interval — use this when the device is
/// not configured to push to /api/hikvision/events.
/// </summary>
public enum HikvisionPollingMode
{
    Fallback = 0,
    Primary  = 1
}

/// <summary>Hikvision integration configuration — bind from appsettings.json "Hikvision" section.</summary>
public sealed class HikvisionOptions
{
    public const string SectionName = "Hikvision";

    /// <summary>Root path for saving JPEG face captures. Default: /app/face-captures</summary>
    public string FaceCaptureBasePath { get; init; } = "/app/face-captures";

    /// <summary>How often the polling service runs (seconds). Default: 120 (2 min).
    /// Tighten to ~60 s when running in <see cref="HikvisionPollingMode.Primary"/> for closer-to-realtime ingest.</summary>
    public int PollingIntervalSeconds { get; init; } = 120;

    /// <summary>Webhook silence threshold — only consulted when <see cref="PollingMode"/> is Fallback.
    /// If no event received for this many minutes, polling activates. Default: 5 minutes.</summary>
    public int WebhookGapMinutes { get; init; } = 5;

    /// <summary>ISAPI call timeout in seconds. Default: 10</summary>
    public int IsapiTimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// Polling strategy. Fallback (default) preserves v2.3.0 behaviour — polling only runs
    /// when the webhook is silent. Primary makes polling unconditional, suitable for deployments
    /// where the Hikvision HTTP Listener is not configured.
    /// </summary>
    public HikvisionPollingMode PollingMode { get; init; } = HikvisionPollingMode.Fallback;
}
