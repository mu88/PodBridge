namespace PodBridge.Api.Authentication;

internal static class AuthenticationApplicationBuilderExtensions
{
    public static WebApplication UsePodBridgeAuthentication(this WebApplication app, bool authEnabled)
    {
        if (authEnabled)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        return app;
    }
}
