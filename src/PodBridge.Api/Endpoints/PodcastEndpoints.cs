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
            .WithTags("Podcasts")
            .WithName("GetPodcastFeed")
            .WithSummary("Gets a cached podcast feed.")
            .WithDescription("Returns RSS 2.0 + iTunes XML by default. Add ?format=json to retrieve the cached feed as JSON.")
            .Produces(StatusCodes.Status200OK, contentType: RssXmlNamespaces.RssMediaType)
            .Produces<PodcastDetailsResponse>(StatusCodes.Status200OK, "application/json")
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status503ServiceUnavailable, contentType: "text/plain")
            .RequireRateLimiting("feed-endpoint");

        endpoints.MapGet("/podcasts", GetPodcasts)
            .WithTags("Podcasts")
            .WithName("GetPodcasts")
            .WithSummary("Lists all configured podcasts.")
            .WithDescription("Returns one JSON item per configured podcast with the public feed URL and a cached or placeholder title.")
            .Produces<IReadOnlyList<PodcastSummaryResponse>>(StatusCodes.Status200OK, "application/json")
            .RequireRateLimiting("podcasts-endpoint");
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

        var response = new PodcastDetailsResponse
        {
            PodcastId = podcastId,
            Title = podcast.Title,
            Description = podcast.Description,
            Author = podcast.Author,
            Language = podcast.Language,
            ImageUrl = podcast.ImageUrl,
            Link = podcast.Link,
            LastUpdated = resolvedPodcast.LastUpdated,
            FeedUrl = feedUrl,
            Episodes = podcast.Episodes
                .OrderByDescending(episode => episode.PublishDate)
                .Select(episode => new PodcastEpisodeResponse
                {
                    Guid = episode.Guid,
                    Title = episode.Title,
                    Description = episode.Description,
                    PublishDate = episode.PublishDate,
                    AudioUrl = episode.AudioUrl,
                    DurationSeconds = episode.DurationSeconds,
                    ImageUrl = episode.ImageUrl,
                    EpisodeNumber = episode.EpisodeNumber,
                    Link = episode.Link,
                })
                .ToList(),
        };

        return Results.Json(response);
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

            return new PodcastSummaryResponse
            {
                PodcastId = podcast.PodcastId,
                Title = title,
                FeedUrl = feedUrl,
            };
        }).ToList();

        return Results.Json(podcastList);
    }
}
