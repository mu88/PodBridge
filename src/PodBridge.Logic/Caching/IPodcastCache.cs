using PodBridge.Logic.Domain;

namespace PodBridge.Logic.Caching;

public interface IPodcastCache
{
    void Update(string podcastId, Podcast podcast);

    CachedPodcast? TryGetFull(string podcastId);
}
