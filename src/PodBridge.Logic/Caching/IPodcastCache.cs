using PodBridge.Logic.Shared;

namespace PodBridge.Logic.Caching;

public interface IPodcastCache
{
    void Update(string podcastId, Podcast podcast);

    CachedPodcast? FindFull(string podcastId);
}
