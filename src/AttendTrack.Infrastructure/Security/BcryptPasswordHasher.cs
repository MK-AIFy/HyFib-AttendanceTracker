using AttendTrack.Domain.Interfaces.Services;

namespace AttendTrack.Infrastructure.Security;

/// <summary>
/// BCrypt implementation of IPasswordHasher.
/// WorkFactor = 12 — used for Hikvision device admin password hashing.
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string plainText)
        => BCrypt.Net.BCrypt.HashPassword(plainText, WorkFactor);

    public bool Verify(string plainText, string hash)
        => BCrypt.Net.BCrypt.Verify(plainText, hash);
}
