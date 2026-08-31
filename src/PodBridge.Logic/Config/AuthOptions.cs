namespace PodBridge.Logic.Config;

public sealed class AuthOptions
{
    public bool Enabled { get; init; } = false;

    // Only the PBKDF2 hash is stored - see PodBridge.Logic.Security.CredentialHasher and
    // Scripts/New-CredentialHash.ps1. The plaintext username/password never needs to be
    // configured anywhere.
    public string UsernameHash { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;
}
