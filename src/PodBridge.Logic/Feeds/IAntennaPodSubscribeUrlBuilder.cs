namespace PodBridge.Logic.Feeds;

public interface IAntennaPodSubscribeUrlBuilder
{
    string BuildSubscribeUrl(string feedUrl);
}
