using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Tests.TestSupport.Http;

internal static partial class HtmlFormHelper
{
    [GeneratedRegex(
        "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex AntiforgeryTokenRegex { get; }

    public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        return (await GetAntiforgeryFormStateAsync(client, path)).Token;
    }

    public static async Task<AntiforgeryFormState> GetAntiforgeryFormStateAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var match = AntiforgeryTokenRegex.Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find antiforgery token on '{path}'.");
        }

        return new AntiforgeryFormState(
            WebUtility.HtmlDecode(match.Groups["token"].Value),
            GetAntiforgeryCookieHeader(response.Headers));
    }

    public static FormUrlEncodedContent CreateLoginContent(string antiforgeryToken, string? username, string? password, string? returnUrl = null)
    {
        var content = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["_handler"] = "login",
            ["Input.Username"] = username ?? string.Empty,
            ["Input.Password"] = password ?? string.Empty,
            ["Input.ReturnUrl"] = returnUrl ?? string.Empty,
        };

        if (!string.IsNullOrWhiteSpace(antiforgeryToken))
        {
            content["__RequestVerificationToken"] = antiforgeryToken;
        }

        return new FormUrlEncodedContent(content);
    }

    public static FormUrlEncodedContent CreateLogoutContent(string antiforgeryToken, string? returnUrl = null)
    {
        var content = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ReturnUrl"] = returnUrl ?? string.Empty,
        };

        if (!string.IsNullOrWhiteSpace(antiforgeryToken))
        {
            content["__RequestVerificationToken"] = antiforgeryToken;
        }

        return new FormUrlEncodedContent(content);
    }

    public static string GetCookieHeader(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders)
            ? string.Join(
                "; ",
                setCookieHeaders.Select(setCookieHeader => setCookieHeader.Split(';', 2)[0]).Where(cookieHeader => !string.IsNullOrWhiteSpace(cookieHeader)))
            : string.Empty;
    }

    private static string GetAntiforgeryCookieHeader(HttpResponseHeaders headers)
    {
        return headers.TryGetValues("Set-Cookie", out var setCookieHeaders)
            ? string.Join(
                "; ",
                setCookieHeaders
                    .Select(setCookieHeader => setCookieHeader.Split(';', 2)[0])
                    .Where(cookieHeader => !string.IsNullOrWhiteSpace(cookieHeader)))
            : string.Empty;
    }
}

internal sealed record AntiforgeryFormState(string Token, string CookieHeader);
