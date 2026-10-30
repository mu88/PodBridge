using System.Diagnostics.CodeAnalysis;

namespace PodBridge.Logic.Feeds;

internal sealed class AntennaPodSubscribeUrlBuilder : IAntennaPodSubscribeUrlBuilder
{
    [SuppressMessage("Maintainability", "S1075:URIs should not be hardcoded", Justification = "This URL defines the AntennaPod subscription deeplink format and is intentionally not configurable.")]
    private const string SubscribeUrlPrefix = "https://antennapod.org/deeplink/subscribe/?url=";

    public string BuildSubscribeUrl(string feedUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedUrl);

        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri) || !IsHttpOrHttps(uri))
        {
            throw new ArgumentException("Feed URL must be an absolute HTTP or HTTPS URI.", nameof(feedUrl));
        }

        return $"{SubscribeUrlPrefix}{Uri.EscapeDataString(feedUrl)}";
    }

    private static bool IsHttpOrHttps(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
