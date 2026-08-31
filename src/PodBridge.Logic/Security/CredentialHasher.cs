using System.Globalization;
using System.Security.Cryptography;

namespace PodBridge.Logic.Security;

/// <summary>
/// Hashes and verifies credentials with PBKDF2 (HMAC-SHA256) so that plaintext usernames/passwords never
/// need to be stored anywhere a deployment platform's dashboard, CI secret store, or config file could
/// expose them - only the irreversible hash is stored. See Scripts/New-CredentialHash.ps1 for the
/// companion tool operators use to generate hashes for the initial setup or a rotation.
/// </summary>
public static class CredentialHasher
{
    public const int Iterations = 210_000;
    public const int SaltSizeInBytes = 16;
    public const int HashSizeInBytes = 32;
    public static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    private const char Separator = '.';

    public static string Hash(string value)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeInBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(value, salt, Iterations, Algorithm, HashSizeInBytes);
        return string.Join(
            Separator,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public static bool Verify(string value, string storedHash)
    {
        var parts = storedHash.Split(Separator, 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(value, salt, iterations, Algorithm, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
