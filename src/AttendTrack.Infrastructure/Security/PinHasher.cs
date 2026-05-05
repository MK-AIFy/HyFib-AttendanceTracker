namespace AttendTrack.Infrastructure.Security;

/// <summary>
/// BCrypt hasher for 6-digit kiosk PINs.
/// WorkFactor = 12 is intentionally high — kiosk PIN attempts are rare and low-volume.
/// </summary>
public static class PinHasher
{
    private const int WorkFactor = 12;

    public static string Hash(string pin)
        => BCrypt.Net.BCrypt.HashPassword(pin, WorkFactor);

    public static bool Verify(string pin, string hash)
        => BCrypt.Net.BCrypt.Verify(pin, hash);
}
