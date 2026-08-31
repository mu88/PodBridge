using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace PodBridge.Api.Authentication;

internal static class ReturnUrlHelper
{
    public static string GetApplicationRoot(PathString pathBase)
    {
        return pathBase.HasValue ? $"{pathBase}/" : "/";
    }

    public static string GetSafeDestination(string? returnUrl, PathString pathBase)
    {
        return IsSafeLocalPath(returnUrl)
            ? returnUrl!
            : GetApplicationRoot(pathBase);
    }

    public static string BuildLoginPath(PathString pathBase, string? returnUrl)
    {
        var loginPath = pathBase.HasValue ? $"{pathBase}/login" : "/login";
        var safeDestination = GetSafeDestination(returnUrl, pathBase);

        return QueryHelpers.AddQueryString(
            loginPath,
            CookieAuthenticationDefaults.ReturnUrlParameter,
            safeDestination);
    }

    private static bool IsSafeLocalPath(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) &&
               returnUrl[0] == '/' &&
               (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'));
    }
}
