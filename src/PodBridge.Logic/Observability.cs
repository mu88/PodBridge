using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace PodBridge.Logic;

[ExcludeFromCodeCoverage]
public static class Observability
{
    public const string MeterName = SourceName;
    public const string PodcastIdTag = "podcast.id";
    public const string PlatformCountTag = "platform.count";
    public const string PodcastCountTag = "podcast.count";
    public const string RefreshSuccessCountTag = "refresh.success_count";
    public const string RefreshFailureCountTag = "refresh.failure_count";

    public static readonly ActivitySource Source = new(SourceName);

    private const string SourceName = "PodBridge";
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RefreshSuccessCounter = Meter.CreateCounter<long>(
        "podbridge.refresh.success",
        description: "Counts successful podcast refreshes.");
    private static readonly Counter<long> RefreshFailureCounter = Meter.CreateCounter<long>(
        "podbridge.refresh.failure",
        description: "Counts failed podcast refreshes.");
    private static readonly Counter<long> EpisodesFetchedCounter = Meter.CreateCounter<long>(
        "podbridge.episodes.fetched",
        description: "Counts fetched episodes per podcast refresh.");

    public static void RecordRefreshSuccess(string podcastId)
    {
        RefreshSuccessCounter.Add(1, CreatePodcastTag(podcastId));
    }

    public static void RecordRefreshFailure(string podcastId)
    {
        RefreshFailureCounter.Add(1, CreatePodcastTag(podcastId));
    }

    public static void RecordEpisodesFetched(string podcastId, int episodeCount)
    {
        EpisodesFetchedCounter.Add(episodeCount, CreatePodcastTag(podcastId));
    }

    private static KeyValuePair<string, object?> CreatePodcastTag(string podcastId)
    {
        return new KeyValuePair<string, object?>(PodcastIdTag, podcastId);
    }
}
