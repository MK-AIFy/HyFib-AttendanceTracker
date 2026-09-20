namespace AttendTrack.Domain.Exceptions;

/// <summary>
/// Thrown by KioskLockoutBehaviour when an employee code has exceeded the
/// maximum failed kiosk PIN attempts within the lockout window (mirrors
/// AuthController's admin-login lockout: 5 failures → 15-minute lock, tracked
/// in Redis via IDistributedCacheWrapper so it survives a Blazor circuit
/// refresh and self-expires without admin intervention).
/// </summary>
public sealed class KioskLockedOutException : DomainException
{
    public DateTime LockedUntilUtc { get; }

    public KioskLockedOutException(DateTime lockedUntilUtc)
        : base(BuildMessage(lockedUntilUtc))
        => LockedUntilUtc = lockedUntilUtc;

    private static string BuildMessage(DateTime lockedUntilUtc)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling((lockedUntilUtc - DateTime.UtcNow).TotalMinutes));
        return $"Too many failed attempts. Try again in {minutes} min.";
    }
}
