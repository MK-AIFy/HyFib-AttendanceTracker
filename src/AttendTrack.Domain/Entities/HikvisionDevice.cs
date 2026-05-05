using AttendTrack.Domain.Common;

namespace AttendTrack.Domain.Entities;

/// <summary>
/// Registry entry for a Hikvision DS-K1T320MFWX biometric access controller.
/// Stores connection details and tracks health/polling state.
/// </summary>
public sealed class HikvisionDevice : AggregateRoot<Guid>
{
    public string    DeviceName             { get; private set; } = default!;
    public string    Model                  { get; private set; } = default!;
    public string    SerialNumber           { get; private set; } = default!;
    public string    IpAddress              { get; private set; } = default!;
    public int       Port                   { get; private set; }
    public string    AdminUsername          { get; private set; } = default!;
    /// <summary>BCrypt hash of device admin password (stored, never plaintext).</summary>
    public string    AdminPasswordHash      { get; private set; } = default!;
    /// <summary>Data-Protection-encrypted password for ISAPI outbound calls (decryptable).</summary>
    public string?   AdminPasswordProtected  { get; private set; }
    public string    Location               { get; private set; } = default!;
    public bool      IsActive               { get; private set; }
    /// <summary>UTC time of the last ISAPI event push received — used to detect webhook gaps.</summary>
    public DateTime? LastEventReceivedAt    { get; private set; }
    public DateTime? LastPollAt             { get; private set; }
    public string    FirmwareVersion        { get; private set; } = default!;
    public int       EnrolledEmployeeCount  { get; private set; }
    public DateTime  RegisteredAt           { get; private set; }

    private HikvisionDevice() { }

    public static HikvisionDevice Register(
        Guid    id,
        string  deviceName,
        string  model,
        string  serialNumber,
        string  ipAddress,
        int     port,
        string  adminUsername,
        string  adminPasswordHash,
        string? adminPasswordProtected,
        string  location,
        string  firmwareVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminPasswordHash);

        return new HikvisionDevice
        {
            Id                    = id,
            DeviceName            = deviceName,
            Model                 = model,
            SerialNumber          = serialNumber,
            IpAddress             = ipAddress,
            Port                  = port,
            AdminUsername         = adminUsername,
            AdminPasswordHash      = adminPasswordHash,
            AdminPasswordProtected = adminPasswordProtected,
            Location              = location,
            FirmwareVersion       = firmwareVersion,
            IsActive              = true,
            EnrolledEmployeeCount = 0,
            RegisteredAt          = DateTime.UtcNow
        };
    }

    public void RecordEventReceived(DateTime eventTimeUtc) => LastEventReceivedAt = eventTimeUtc;
    public void RecordPoll(DateTime pollTimeUtc)           => LastPollAt = pollTimeUtc;
    public void IncrementEnrolledCount()                   => EnrolledEmployeeCount++;
    public void DecrementEnrolledCount()                   => EnrolledEmployeeCount = Math.Max(0, EnrolledEmployeeCount - 1);
    public void Deactivate()                               => IsActive = false;

    public void UpdateFirmware(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        FirmwareVersion = version;
    }
}
