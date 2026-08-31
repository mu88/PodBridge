using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PodBridge.Logic.Config;

namespace PodBridge.Api.Authentication;

public sealed class BasicAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<PodBridgeOptions> podBridgeOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Basic";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValue) ||
            !AuthenticationHeaderValue.TryParse(headerValue, out var authHeader) ||
            !string.Equals(authHeader.Scheme, SchemeName, StringComparison.OrdinalIgnoreCase) ||
            authHeader.Parameter is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing or malformed Authorization header"));
        }

        byte[] credentialBytes;
        try
        {
            credentialBytes = Convert.FromBase64String(authHeader.Parameter);
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Authorization header is not valid Base64"));
        }

        var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
        if (credentials.Length != 2 || !IsValidCredentials(credentials[0], credentials[1]))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid username or password"));
        }

        var principal = PodBridgeClaimsPrincipalFactory.Create(SchemeName, credentials[0]);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"PodBridge\", charset=\"UTF-8\"";
        return base.HandleChallengeAsync(properties);
    }

    private bool IsValidCredentials(string username, string password)
    {
        return ConfiguredCredentialValidator.AreValid(podBridgeOptions.Value.Auth, username, password);
    }
}
