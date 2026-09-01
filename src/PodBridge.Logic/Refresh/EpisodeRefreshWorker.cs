using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PodBridge.Logic.Caching;
using PodBridge.Logic.Config;
using PodBridge.Logic.EpisodeSourcing;

namespace PodBridge.Logic.Refresh;

internal sealed partial class EpisodeRefreshWorker(
    IEpisodeSource episodeSource,
    IPodcastCache podcastCache,
    IOptions<PodBridgeOptions> options,
    TimeProvider timeProvider,
    ILogger<EpisodeRefreshWorker> logger) : BackgroundService
{
    private readonly Func<bool> _continueLoop = () => true;

    /// <summary>
    /// Test-only seam: allows unit tests to make <see cref="ExecuteAsync"/> exit its otherwise-infinite
    /// refresh loop deterministically (e.g. after a single tick), so the method's normal, non-cancelled
    /// completion path can be exercised without relying on <see cref="PeriodicTimer"/> disposal races.
    /// </summary>
    internal EpisodeRefreshWorker(
        IEpisodeSource episodeSource,
        IPodcastCache podcastCache,
        IOptions<PodBridgeOptions> options,
        TimeProvider timeProvider,
        ILogger<EpisodeRefreshWorker> logger,
        Func<bool> continueLoop)
        : this(episodeSource, podcastCache, options, timeProvider, logger)
    {
        _continueLoop = continueLoop;
    }

    internal async Task RefreshAllShowsAsync(CancellationToken cancellationToken)
    {
        var podcasts = options.Value.Podcasts;
        var platformCount = podcasts.Count > 0 ? 1 : 0;
        var podcastCount = podcasts.Count;

        using var activity = Observability.Source.StartActivity();
        activity?.SetTag(Observability.PlatformCountTag, platformCount);
        activity?.SetTag(Observability.PodcastCountTag, podcastCount);

        var (successfulPodcastCount, failedPodcastCount) = await RefreshPodcastsAsync(podcasts, cancellationToken);
        activity?.SetTag(Observability.RefreshSuccessCountTag, successfulPodcastCount);
        activity?.SetTag(Observability.RefreshFailureCountTag, failedPodcastCount);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.BackgroundRefreshEnabled)
        {
            LogBackgroundRefreshDisabled();
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.RefreshIntervalMinutes), timeProvider);

        do
        {
            await RefreshAllShowsAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken) && _continueLoop());
    }

    private async Task<(int SuccessfulPodcastCount, int FailedPodcastCount)> RefreshPodcastsAsync(
        IReadOnlyList<PodcastConfig> podcasts,
        CancellationToken cancellationToken)
    {
        var successfulPodcastCount = 0;
        var failedPodcastCount = 0;

        foreach (var podcast in podcasts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await TryRefreshPodcastAsync(podcast, cancellationToken))
            {
                successfulPodcastCount++;
            }
            else
            {
                failedPodcastCount++;
            }
        }

        return (successfulPodcastCount, failedPodcastCount);
    }

    private async Task<bool> TryRefreshPodcastAsync(PodcastConfig podcast, CancellationToken cancellationToken)
    {
        using var activity = Observability.Source.StartActivity("RefreshPodcast");
        activity?.SetTag(Observability.PodcastIdTag, podcast.PodcastId);

        try
        {
            var resolvedPodcast = await episodeSource.FetchEpisodesAsync(podcast, cancellationToken);
            Observability.RecordEpisodesFetched(podcast.PodcastId, resolvedPodcast.Episodes.Count);
            podcastCache.Update(podcast.PodcastId, resolvedPodcast);
            Observability.RecordRefreshSuccess(podcast.PodcastId);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            Observability.RecordRefreshFailure(podcast.PodcastId);
            LogRefreshFailed(exception, podcast.PodcastId);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to refresh podcast {PodcastId}")]
    private partial void LogRefreshFailed(Exception exception, string podcastId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Background podcast refresh is disabled (PodBridge:BackgroundRefreshEnabled=false)")]
    private partial void LogBackgroundRefreshDisabled();
}
