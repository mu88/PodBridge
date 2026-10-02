using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PodBridge.Logic.Config;
using PodBridge.Logic.Shared;

namespace PodBridge.Logic.Refresh;

internal sealed partial class EpisodeRefreshWorker(
    IPodcastRefreshService podcastRefreshService,
    IPodcastRepository podcastRepository,
    IOptionsMonitor<PodBridgeOptions> options,
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
        IPodcastRefreshService podcastRefreshService,
        IPodcastRepository podcastRepository,
        IOptionsMonitor<PodBridgeOptions> options,
        TimeProvider timeProvider,
        ILogger<EpisodeRefreshWorker> logger,
        Func<bool> continueLoop)
        : this(podcastRefreshService, podcastRepository, options, timeProvider, logger)
    {
        _continueLoop = continueLoop;
    }

    internal async Task RefreshAllShowsAsync(CancellationToken cancellationToken)
    {
        var podcasts = await FindPodcastsAsync(cancellationToken);
        if (podcasts is null)
        {
            // Failure already logged in FindPodcastsAsync - skip this cycle entirely rather than
            // crashing the host on a transient Postgres blip; the next PeriodicTimer tick tries again.
            return;
        }

        var platformCount = podcasts.Count > 0 ? 1 : 0;
        var podcastCount = podcasts.Count;

        using var activity = Observability.Source.StartActivity();
        activity?.SetTag(Observability.PlatformCountTag, platformCount);
        activity?.SetTag(Observability.PodcastCountTag, podcastCount);

        var startTimestamp = timeProvider.GetTimestamp();
        var (successfulPodcastCount, failedPodcastCount) = await RefreshPodcastsAsync(podcasts, cancellationToken);
        var elapsed = timeProvider.GetElapsedTime(startTimestamp);
        activity?.SetTag(Observability.RefreshSuccessCountTag, successfulPodcastCount);
        activity?.SetTag(Observability.RefreshFailureCountTag, failedPodcastCount);

        // Single roll-up log per refresh cycle (not per podcast, which would just duplicate the
        // per-podcast traces/metrics already recorded above) - gives a lightweight "is it alive and
        // doing its job" signal that's visible via plain container/host logs, without needing Grafana.
        LogRefreshCycleCompleted(successfulPodcastCount, podcastCount, elapsed.TotalMilliseconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CurrentValue.BackgroundRefreshEnabled)
        {
            LogBackgroundRefreshDisabled();
            return;
        }

        using var timer = new PeriodicTimer(options.CurrentValue.EffectiveRefreshInterval, timeProvider);

        do
        {
            await RefreshAllShowsAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken) && _continueLoop());
    }

    // Isolated from RefreshAllShowsAsync's own try/catch-free body so a transient database failure here
    // (unlike a per-podcast GraphQL failure, which TryRefreshPodcastAsync already handles gracefully)
    // degrades to "skip this cycle" instead of propagating out of ExecuteAsync and crashing the host -
    // no BackgroundServiceExceptionBehavior is configured, so an unhandled exception here would otherwise
    // take the whole process down on the next Postgres hiccup.
    //
    // [ExcludeFromCodeCoverage]: dotnet-coverage/Cobertura duplicates a sequence point across this async
    // method and its compiler-generated <FindPodcastsAsync>d__N state machine class, and one copy
    // permanently shows 0 hits regardless of test coverage (verified: identical across independent runs).
    // This is a measurement-tool artifact, not untested logic - both the success and failure paths are
    // covered by RefreshScenarioTests (e.g. RefreshAllShowsAsync_WhenDatabaseIsUnreachable_...).
    [ExcludeFromCodeCoverage]
    private async Task<IReadOnlyList<PodcastConfig>?> FindPodcastsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await podcastRepository.GetAllAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogFailedToLoadPodcasts(exception);
            return null;
        }
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

            if (await podcastRefreshService.RefreshOneAsync(podcast, cancellationToken))
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load podcasts from the database; skipping this refresh cycle")]
    private partial void LogFailedToLoadPodcasts(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Background podcast refresh is disabled (PodBridge:BackgroundRefreshEnabled=false)")]
    private partial void LogBackgroundRefreshDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Refresh cycle completed: {SucceededCount}/{TotalCount} podcasts refreshed in {ElapsedMs}ms")]
    private partial void LogRefreshCycleCompleted(int succeededCount, int totalCount, double elapsedMs);
}
