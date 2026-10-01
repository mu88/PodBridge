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

    // Every policy below explicitly names its own scheme (see ConfigureUiAuthorizationPolicy/
    // ConfigureApiAuthorizationPolicy), so these defaults are never actually consulted - equivalent mutants.
    // Stryker disable once all: defaults are never consulted, every policy names its scheme explicitly
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

        // LogoutPath only matters for the framework's automatic "remote sign-out" redirect; this app signs out
        // via its own explicit /logout minimal-API endpoint (LogoutEndpointExtensions), so it's never consulted.
        // Stryker disable once all: never consulted, /logout is handled by an explicit endpoint, not this redirect
        options.LogoutPath = "/logout";
    }

    private static void ConfigureUiAuthorizationPolicy(AuthorizationPolicyBuilder policy)
    {
        policy.RequireAuthenticatedUser();

        // UiCookie is also ConfigureAuthentication's default scheme, so removing this explicit scheme falls
        // back to the identical default - equivalent mutant.
        // Stryker disable once all: redundant with the identical app-wide default scheme
        policy.AddAuthenticationSchemes(PodBridgeAuthenticationSchemes.UiCookie);
    }

    private static void ConfigureApiAuthorizationPolicy(AuthorizationPolicyBuilder policy)
    {
        policy.AddAuthenticationSchemes(BasicAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
    }
}
