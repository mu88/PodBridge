using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PodBridge.Logic.Caching;
using PodBridge.Logic.Config;
using PodBridge.Logic.EpisodeSourcing;

namespace PodBridge.Logic.Refresh;

/// <summary>
/// Fetches and caches episodes for a single podcast. Extracted from <see cref="EpisodeRefreshWorker"/> so the
/// same refresh logic can be reused both by the periodic background loop and by the "Add Podcast" flow, which
/// needs to seed a newly-added podcast's episodes immediately instead of waiting for the next periodic tick.
/// </summary>
public interface IPodcastRefreshService
{
    Task<bool> RefreshOneAsync(PodcastConfig podcast, CancellationToken cancellationToken);
}

internal sealed partial class PodcastRefreshService(
    IEpisodeSource episodeSource,
    IPodcastCache podcastCache,
    IEpisodeRefreshHealthState healthState,
    ILogger<PodcastRefreshService> logger) : IPodcastRefreshService
{
    public async Task<bool> RefreshOneAsync(PodcastConfig podcast, CancellationToken cancellationToken)
    {
        using var activity = Observability.Source.StartActivity("RefreshPodcast");
        activity?.SetTag(Observability.PodcastIdTag, podcast.PodcastId);

        try
        {
            var resolvedPodcast = await episodeSource.FetchEpisodesAsync(podcast, cancellationToken);
            Observability.RecordEpisodesFetched(podcast.PodcastId, resolvedPodcast.Episodes.Count);
            podcastCache.Update(podcast.PodcastId, resolvedPodcast);
            Observability.RecordRefreshSuccess(podcast.PodcastId);
            healthState.RecordResult(podcast.PodcastId, succeeded: true);
            return true;
        }

        // An OperationCanceledException is only a genuine shutdown/cancellation request when it was raised
        // for the CancellationToken this method was actually given - propagate that case unchanged so the
        // BackgroundService shutdown semantics keep working. Any other OperationCanceledException (e.g. an
        // HttpClient request timeout, which throws TaskCanceledException regardless of the caller's token)
        // must be treated as a regular refresh failure instead of silently escaping the try/catch: letting it
        // propagate would abort the whole refresh cycle and, since no BackgroundServiceExceptionBehavior is
        // configured, crash the entire host on the next transient GraphQL timeout.
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            Observability.RecordRefreshFailure(podcast.PodcastId);
            healthState.RecordResult(podcast.PodcastId, succeeded: false);
            LogRefreshFailed(exception, podcast.PodcastId);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to refresh podcast {PodcastId}")]
    private partial void LogRefreshFailed(Exception exception, string podcastId);
}
