using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using PodBridge.Logic.Config;

namespace PodBridge.Api.Authentication;

internal static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddPodBridgeAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(ConfigureAuthentication)
            .AddCookie(PodBridgeAuthenticationSchemes.UiCookie, ConfigureCookie)
            .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthenticationHandler.SchemeName, null);

        services.AddAuthorizationBuilder()
            .AddPolicy(PodBridgeAuthorizationPolicies.Ui, ConfigureUiAuthorizationPolicy)
            .AddPolicy(PodBridgeAuthorizationPolicies.Api, ConfigureApiAuthorizationPolicy);

        return services;
    }

    private static void ConfigureAuthentication(AuthenticationOptions options)
    {
        options.DefaultAuthenticateScheme = PodBridgeAuthenticationSchemes.UiCookie; // NOSONAR
        options.DefaultChallengeScheme = PodBridgeAuthenticationSchemes.UiCookie;
        options.DefaultSignInScheme = PodBridgeAuthenticationSchemes.UiCookie;
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = "PodBridge.UiAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
    }

    private static void ConfigureUiAuthorizationPolicy(AuthorizationPolicyBuilder policy)
    {
        policy.AddAuthenticationSchemes(PodBridgeAuthenticationSchemes.UiCookie);
        policy.RequireAuthenticatedUser();
    }

    private static void ConfigureApiAuthorizationPolicy(AuthorizationPolicyBuilder policy)
    {
        policy.AddAuthenticationSchemes(BasicAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
    }
}
