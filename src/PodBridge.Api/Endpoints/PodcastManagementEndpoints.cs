using PodBridge.Logic.EpisodeSourcing;
using PodBridge.Logic.PodcastManagement;

namespace PodBridge.Api.Endpoints;

internal static class PodcastManagementEndpoints
{
    private const string JsonContentType = "application/json";

    public static void MapPodcastManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/podcasts/search", SearchPodcasts)
            .WithTags("Podcasts")
            .WithName("SearchPodcasts")
            .WithSummary("Searches the podcast directory by show title.")
            .WithDescription("Returns matching shows (ShowId, title, episode count) that can be passed to POST /podcasts to add one.")
            .Produces<IReadOnlyList<PodcastSearchResultResponse>>(StatusCodes.Status200OK, JsonContentType)
            .Produces(StatusCodes.Status502BadGateway, contentType: "text/plain")
            .RequireRateLimiting("podcasts-endpoint");

        endpoints.MapPost("/podcasts", AddPodcast)
            .WithTags("Podcasts")
            .WithName("AddPodcast")
            .WithSummary("Adds a podcast by ShowId.")
            .WithDescription(
                "Resolves the show's title via the podcast directory unless Title is already supplied (e.g. from " +
                "a prior search result), then persists it and triggers the initial episode backfill in the background.")
            .Produces<AddPodcastResponse>(StatusCodes.Status201Created, JsonContentType)
            .Produces<AddPodcastResponse>(StatusCodes.Status404NotFound, JsonContentType)
            .Produces<AddPodcastResponse>(StatusCodes.Status409Conflict, JsonContentType)
            .Produces<AddPodcastResponse>(StatusCodes.Status502BadGateway, JsonContentType)
            .RequireRateLimiting("podcasts-endpoint");
    }

    private static async Task<IResult> SearchPodcasts(
        string query,
        IPodcastDirectory podcastDirectory,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PodcastSearchResult> results;
        try
        {
            results = await podcastDirectory.SearchAsync(query, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            return Results.Text(
                "Podcast search failed - please try again shortly.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        var response = results
            .Select(result => new PodcastSearchResultResponse
            {
                ShowId = result.ShowId,
                Title = result.Title,
                EpisodeCount = result.EpisodeCount,
            })
            .ToList();

        return Results.Json(response);
    }

    private static async Task<IResult> AddPodcast(
        AddPodcastRequest request,
        IPodcastManagementService podcastManagementService,
        CancellationToken cancellationToken)
    {
        var result = string.IsNullOrWhiteSpace(request.Title)
            ? await podcastManagementService.AddByShowIdAsync(request.ShowId, cancellationToken)
            : await podcastManagementService.AddAsync(request.ShowId, request.Title, cancellationToken);

        var response = new AddPodcastResponse
        {
            Status = result.Status.ToString(),
            Title = result.Title,
            PodcastId = result.PodcastId,
        };

        return result.Status switch
        {
            PodcastAddStatus.Added => Results.Json(response, statusCode: StatusCodes.Status201Created),
            PodcastAddStatus.ShowIdAlreadyExists => Results.Json(response, statusCode: StatusCodes.Status409Conflict),
            PodcastAddStatus.ShowIdNotFound => Results.Json(response, statusCode: StatusCodes.Status404NotFound),
            _ => Results.Json(response, statusCode: StatusCodes.Status502BadGateway),
        };
    }
}
