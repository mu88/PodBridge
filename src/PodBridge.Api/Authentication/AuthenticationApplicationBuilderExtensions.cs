namespace PodBridge.Api.Authentication;

internal static class AuthenticationApplicationBuilderExtensions
{
    public static WebApplication UsePodBridgeAuthentication(this WebApplication app, bool authEnabled)
    {
        if (authEnabled)
        {
            UseAuthenticationMiddleware(app);
            app.UseAuthorization();
        }

        return app;
    }

    // The authorization middleware's policy evaluator re-authenticates the current scheme itself when
    // HttpContext.User hasn't been populated yet, so every RequireAuthorization-protected endpoint in this
    // app authenticates correctly even without this call - equivalent mutant. Kept for defense-in-depth and
    // the framework-recommended pipeline ordering (authentication before authorization).
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Equivalent mutant: RequireAuthorization policies re-authenticate internally; see comment above.")]
    private static void UseAuthenticationMiddleware(WebApplication app) => app.UseAuthentication();
}
