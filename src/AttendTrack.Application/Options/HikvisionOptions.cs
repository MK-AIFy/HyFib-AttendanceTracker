namespace AttendTrack.Application.Options;

/// <summary>Hikvision integration configuration — bind from appsettings.json "Hikvision" section.</summary>
public sealed class HikvisionOptions
{
    public const string SectionName = "Hikvision";

    /// <summary>Root path for saving JPEG face captures. Default: /app/face-captures</summary>
    public string FaceCaptureBasePath { get; init; } = "/app/face-captures";

    /// <summary>How often the fallback polling service runs (seconds). Default: 120 (2 min)</summary>
    public int PollingIntervalSeconds { get; init; } = 120;

    /// <summary>Webhook silence threshold — if no event received for this many minutes,
    /// the polling service activates as fallback. Default: 5 minutes.</summary>
    public int WebhookGapMinutes { get; init; } = 5;

    /// <summary>ISAPI call timeout in seconds. Default: 10</summary>
    public int IsapiTimeoutSeconds { get; init; } = 10;
}
