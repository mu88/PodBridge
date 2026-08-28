using Microsoft.Extensions.Options;
using PodBridge.Api.Rss;
using PodBridge.Logic.Caching;
using PodBridge.Logic.Config;
using PodBridge.Logic.Feeds;

namespace PodBridge.Api.Endpoints;

internal static class PodcastEndpoints
{
    public static void MapPodcastEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/podcasts/{podcastId}", GetPodcastFeed)
            .RequireRateLimiting("feed-endpoint");

        endpoints.MapGet("/podcasts", GetPodcasts)
            .RequireRateLimiting("podcasts-endpoint")
            .WithName("GetPodcasts");
    }

    private static IResult GetPodcastFeed(
        string podcastId,
        HttpRequest request,
        IPodcastCache podcastCache,
        IFeedUrlBuilder feedUrlBuilder,
        IOptions<PodBridgeOptions> opts)
    {
        // Check config first (404 if never configured), then cache (503 if configured but not yet fetched)
        var podcastConfig = opts.Value.Podcasts
            .FirstOrDefault(configuredPodcast => string.Equals(configuredPodcast.PodcastId, podcastId, StringComparison.Ordinal));
        if (podcastConfig is null)
        {
            return Results.NotFound();
        }

        var resolvedPodcast = podcastCache.TryGetFull(podcastId);
        if (resolvedPodcast is null)
        {
            return Results.Text("Feed not yet generated, please retry shortly.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var feedUrl = feedUrlBuilder.BuildFeedUrl(podcastId, request.ToBaseUrl());

        // RSS/XML is the default representation because the primary consumers are podcatchers, which
        // identify the feed by Content-Type rather than by sending an "Accept: application/json" header.
        // JSON is an explicit opt-in via ?format=json for API/debugging consumers.
        return string.Equals(request.Query["format"].ToString(), "json", StringComparison.OrdinalIgnoreCase)
            ? BuildJsonFeed(podcastId, resolvedPodcast, feedUrl)
            : BuildXmlFeed(resolvedPodcast, feedUrl);
    }

    private static IResult BuildJsonFeed(string podcastId, CachedPodcast resolvedPodcast, string feedUrl)
    {
        var podcast = resolvedPodcast.Podcast;
        return Results.Json(new
        {
            podcastId,
            podcast.Title,
            podcast.Description,
            podcast.Author,
            podcast.Language,
            podcast.ImageUrl,
            podcast.Link,
            resolvedPodcast.LastUpdated,
            feedUrl,
            episodes = podcast.Episodes
                .OrderByDescending(episode => episode.PublishDate)
                .Select(episode => new
                {
                    episode.Guid,
                    episode.Title,
                    episode.Description,
                    episode.PublishDate,
                    episode.AudioUrl,
                    episode.DurationSeconds,
                    episode.ImageUrl,
                    episode.EpisodeNumber,
                    episode.Link,
                }),
        });
    }

    private static IResult BuildXmlFeed(CachedPodcast resolvedPodcast, string feedUrl)
    {
        var feedXml = RssFeedSerializer.Serialize(RssFeed.MapFrom(resolvedPodcast.Podcast), feedUrl);
        return Results.Text(feedXml, RssXmlNamespaces.RssMediaType);
    }

    private static IResult GetPodcasts(
        HttpRequest request,
        IPodcastCache podcastCache,
        IFeedUrlBuilder feedUrlBuilder,
        IOptions<PodBridgeOptions> opts)
    {
        // Iterate config to show all configured podcasts (even not-yet-fetched), enrich per-row from cache if available
        var podcastList = opts.Value.Podcasts.Select(podcast =>
        {
            var feedUrl = feedUrlBuilder.BuildFeedUrl(podcast.PodcastId, request.ToBaseUrl());
            var cached = podcastCache.TryGetFull(podcast.PodcastId);
            var title = cached?.Podcast.Title ?? $"Podcast {podcast.ShowId} - not yet fetched";

            return new
            {
                podcastId = podcast.PodcastId,
                title,
                feedUrl,
            };
        }).ToList();

        return Results.Json(podcastList);
    }
}
