using System.Collections.Concurrent;

namespace PodBridge.Logic.Refresh;

internal sealed class EpisodeRefreshHealthState : IEpisodeRefreshHealthState
{
    private readonly ConcurrentDictionary<string, bool> _lastResultByPodcastId = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> GetFailingPodcastIds()
    {
        return _lastResultByPodcastId
            .Where(entry => !entry.Value)
            .Select(entry => entry.Key)
            .ToList();
    }

    public void RecordResult(string podcastId, bool succeeded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(podcastId);
        _lastResultByPodcastId[podcastId] = succeeded;
    }
}
