namespace AttendTrack.Domain.ValueObjects;

/// <summary>
/// BCrypt hash of a 6-digit kiosk PIN.
/// The raw PIN never leaves the application boundary — only the hash is stored.
/// </summary>
public sealed record PinHash
{
    public string Value { get; init; }

    private PinHash(string value) => Value = value;

    public static PinHash From(string bcryptHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bcryptHash);
        return new PinHash(bcryptHash);
    }

    /// <summary>Never expose the hash in logs or serialised responses.</summary>
    public override string ToString() => "[REDACTED]";
}
