using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using PodBridge.Logic.Config;

namespace PodBridge.Api.Authentication;

internal static class LogoutEndpointExtensions
{
    public static WebApplication MapPodBridgeLogoutEndpoint(this WebApplication app, PodBridgeOptions resolvedOptions)
    {
        if (!resolvedOptions.Auth.Enabled)
        {
            return app;
        }

        app.MapPost("/logout", HandleLogoutAsync)
            .DisableAntiforgery()
            .RequireAuthorization(PodBridgeAuthorizationPolicies.Ui);

        return app;
    }

    private static async Task<IResult> HandleLogoutAsync([FromForm] LogoutRequest request, HttpContext context)
    {
        await context.SignOutAsync(PodBridgeAuthenticationSchemes.UiCookie);
        return TypedResults.Redirect(ReturnUrlHelper.BuildLoginPath(context.Request.PathBase, request.ReturnUrl));
    }
}
