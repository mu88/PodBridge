using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
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
        // Antiforgery (required by Blazor's Razor Components endpoints) calls AuthenticateAsync() on every
        // request regardless of whether app.UseAuthentication() is wired into the pipeline. When Auth is
        // disabled, NoResult (rather than Fail) avoids logging misleading "authentication failed" messages
        // for requests that were never expected to carry credentials in the first place.
        if (!podBridgeOptions.Value.Auth.Enabled)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

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

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, credentials[0])], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"PodBridge\", charset=\"UTF-8\"";
        return base.HandleChallengeAsync(properties);
    }

    private static bool FixedTimeEquals(string actual, string expected)
    {
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    private bool IsValidCredentials(string username, string password)
    {
        var configuredAuth = podBridgeOptions.Value.Auth;
        return FixedTimeEquals(username, configuredAuth.Username) && FixedTimeEquals(password, configuredAuth.Password);
    }
}
