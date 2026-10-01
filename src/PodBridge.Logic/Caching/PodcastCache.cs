using System.Collections.Concurrent;
using PodBridge.Logic.Shared;

namespace PodBridge.Logic.Caching;

internal sealed class PodcastCache(TimeProvider timeProvider) : IPodcastCache
{
    private readonly ConcurrentDictionary<string, CachedPodcast> _cache = new(StringComparer.Ordinal);

    public void Update(string podcastId, Podcast podcast)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(podcastId);
        ArgumentNullException.ThrowIfNull(podcast);
        _cache[podcastId] = new CachedPodcast(podcast, timeProvider.GetUtcNow());
    }

    public CachedPodcast? FindFull(string podcastId)
    {
        return _cache.GetValueOrDefault(podcastId);
    }
}
