namespace PodBridge.Logic.Feeds;

public interface IFeedUrlBuilder
{
    string BuildFeedUrl(string podcastId, string requestBaseUrl);
}
