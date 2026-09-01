using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;

namespace PodBridge.Api.Authentication;

internal static class ReturnUrlHelper
{
    private const string ApplicationRoot = "/";

    public static string GetSafeDestination(string? returnUrl)
    {
        return IsSafeLocalPath(returnUrl)
            ? returnUrl!
            : ApplicationRoot;
    }

    public static string BuildLoginPath(string? returnUrl)
    {
        return QueryHelpers.AddQueryString(
            "/login",
            CookieAuthenticationDefaults.ReturnUrlParameter,
            GetSafeDestination(returnUrl));
    }

    private static bool IsSafeLocalPath(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) &&
               returnUrl[0] == '/' &&
               (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'));
    }
}
