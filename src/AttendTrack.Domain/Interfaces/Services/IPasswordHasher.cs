namespace AttendTrack.Domain.Interfaces.Services;

/// <summary>
/// Abstraction for one-way password hashing.
/// Used by Application handlers; implemented in Infrastructure using BCrypt.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Produces a BCrypt hash of <paramref name="plainText"/>.</summary>
    string Hash(string plainText);

    /// <summary>Returns true if <paramref name="plainText"/> matches <paramref name="hash"/>.</summary>
    bool Verify(string plainText, string hash);
}
