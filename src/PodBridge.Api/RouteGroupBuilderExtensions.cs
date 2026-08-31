namespace PodBridge.Api;

internal static class RouteGroupBuilderExtensions
{
    public static RouteGroupBuilder MapPodBridgeProtectedGroup(this WebApplication app, string prefix, string policyName, bool authEnabled)
    {
        return authEnabled
            ? app.MapGroup(prefix).RequireAuthorization(policyName)
            : app.MapGroup(prefix);
    }
}
