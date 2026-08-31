using PodBridge.Logic.Config;
using PodBridge.Logic.Security;

namespace PodBridge.Api.Authentication;

internal static class ConfiguredCredentialValidator
{
    public static bool AreValid(AuthOptions configuredAuth, string username, string password)
    {
        // Evaluate both hashes unconditionally (no short-circuiting `&&`) so an incorrect username
        // doesn't finish faster than an incorrect password - avoids leaking username validity via
        // response timing.
        var isUsernameValid = CredentialHasher.Verify(username, configuredAuth.UsernameHash);
        var isPasswordValid = CredentialHasher.Verify(password, configuredAuth.PasswordHash);

        // The bitwise '&' below is intentional, not a typo for '&&': using logical AND would
        // short-circuit and skip verifying the password whenever the username is already wrong,
        // which is exactly the timing side-channel this method exists to avoid.
#pragma warning disable S2178 // Short-circuit logic should be used in boolean contexts
        return isUsernameValid & isPasswordValid;
#pragma warning restore S2178
    }
}
