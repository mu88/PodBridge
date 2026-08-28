namespace PodBridge.Api;

internal static class HttpRequestExtensions
{
    public static string ToBaseUrl(this HttpRequest request)
    {
        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }
}
