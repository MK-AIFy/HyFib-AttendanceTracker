namespace AttendTrack.Domain.Exceptions;

/// <summary>
/// Thrown when a kiosk request originates from an IP not in the whitelist
/// (Gap 3 — kiosk IP spoofing protection).
/// </summary>
public sealed class UnauthorizedKioskException : DomainException
{
    public string RemoteIpAddress { get; }

    public UnauthorizedKioskException(string remoteIpAddress)
        : base($"Kiosk access denied from IP {remoteIpAddress}. Source IP is not whitelisted.")
        => RemoteIpAddress = remoteIpAddress;

    public UnauthorizedKioskException(string remoteIpAddress, string message)
        : base(message)
        => RemoteIpAddress = remoteIpAddress;
}
