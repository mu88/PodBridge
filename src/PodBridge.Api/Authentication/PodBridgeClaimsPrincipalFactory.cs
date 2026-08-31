using System.Security.Claims;

namespace PodBridge.Api.Authentication;

// Shared by BasicAuthenticationHandler (API requests) and Login.razor (UI sign-in) so both entry
// points build an equivalent, minimal name-only principal instead of duplicating the same three lines.
internal static class PodBridgeClaimsPrincipalFactory
{
    public static ClaimsPrincipal Create(string schemeName, string username)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, username)], schemeName);
        return new ClaimsPrincipal(identity);
    }
}
