namespace PodBridge.Logic.Feeds;

internal sealed class FeedUrlBuilder : IFeedUrlBuilder
{
    public string BuildFeedUrl(string podcastId, string requestBaseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(podcastId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestBaseUrl);

        return $"{requestBaseUrl.TrimEnd('/')}/api/podcasts/{podcastId}";
    }
}
