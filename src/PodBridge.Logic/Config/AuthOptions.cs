namespace PodBridge.Logic.Config;

public sealed class AuthOptions
{
    public bool Enabled { get; init; } = false;

    // Only the PBKDF2 hash is stored - see PodBridge.Logic.Security.CredentialHasher and
    // Scripts/New-CredentialHash.ps1. The plaintext username/password never needs to be
    // configured anywhere.
    public string UsernameHash { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;

    // Deliberately separate from PodBridgeOptions.RateLimitingPermitLimit/RateLimitingWindowMinutes:
    // brute-force login protection needs a much stricter threshold than legitimate podcatcher API
    // polling, so the two must be independently configurable. Not [Range]-validated here because
    // PodBridgeOptions.ValidateDataAnnotations() doesn't recurse into nested options objects; an
    // out-of-range value is instead rejected by ASP.NET Core's RateLimiter at startup.
    public int RateLimitingPermitLimit { get; init; } = 5;

    public int RateLimitingWindowMinutes { get; init; } = 15;
}
