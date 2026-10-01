namespace PodBridge.Logic.Refresh;

/// <summary>
/// Tracks the outcome of the most recent refresh attempt per podcast, so <see cref="EpisodeRefreshHealthCheck"/>
/// can report refresh problems without making its own network call against the GraphQL backend.
/// </summary>
public interface IEpisodeRefreshHealthState
{
    IReadOnlyCollection<string> GetFailingPodcastIds();

    void RecordResult(string podcastId, bool succeeded);
}
