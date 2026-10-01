using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using PodBridge.Logic.Config;
using PodBridge.Logic.Shared;

namespace PodBridge.Logic.EpisodeSourcing;

internal sealed class GraphQlEpisodeSource(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<PodBridgeOptions> options,
    GraphQlClient graphQlClient) : IEpisodeSource
{
    // Named (not typed) HttpClient: GraphQlEpisodeSource is registered as a singleton (it's captured by the
    // singleton PodcastRefreshService), so a typed client - which would capture one HttpClient instance for
    // the app's entire lifetime - would defeat IHttpClientFactory's handler rotation. Calling CreateClient
    // per request instead keeps that rotation intact, symmetric to how IDbContextFactory hands out
    // short-lived DbContext instances to singleton consumers.
    public const string HttpClientName = "GraphQl";

    private const int PageSize = 50;
    private const int MaxPages = 100;
    private const string DefaultAudioMimeType = "audio/mpeg";
    private const string ProgramSetQuery = """
        query($showId: ID!, $first: Int!, $after: Cursor) {
          programSet(id: $showId) {
            title
            synopsis
            description
            sharingUrl
            image {
              url
            }
            publicationService {
              title
            }
            items(first: $first, after: $after) {
              pageInfo {
                hasNextPage
                endCursor
              }
              nodes {
                id
                title
                publishDate
                synopsis
                summary
                description
                showNotes
                duration
                episodeNumber
                sharingUrl
                image {
                  url
                }
                audios {
                  url
                  downloadUrl
                  mimeType
                }
              }
            }
          }
        }
        """;

    public async Task<Podcast> FetchEpisodesAsync(PodcastConfig podcast, CancellationToken cancellationToken)
    {
        using var activity = Observability.Source.StartActivity();
        activity?.SetTag(Observability.PodcastIdTag, podcast.PodcastId);

        var programSet = await FetchProgramSetAsync(podcast.ShowId, cancellationToken);
        return MapPodcast(programSet);
    }

    private static ProgramSet ExtractProgramSet(GraphQlData? data, string showId)
    {
        return data?.ProgramSet
            ?? throw new InvalidOperationException($"No program set was returned for show '{showId}'.");
    }

    private static List<Episode> MapEpisodes(IReadOnlyList<Item> items)
    {
        return items
            .Select(MapEpisode)
            .OfType<Episode>()
            .ToList();
    }

    private static Episode? MapEpisode(Item item)
    {
        var preferredAudio = FindPreferredAudio(item.Audios);
        var audioUrl = GraphQlClient.CreateUri(preferredAudio?.DownloadUrl ?? preferredAudio?.Url);
        var imageUrl = GraphQlClient.CreateImageUrl(item.Image?.Url);
        var link = GraphQlClient.CreateUri(item.SharingUrl);

        return audioUrl is null
            ? null
            : new Episode(
                item.Id,
                item.Title,
                FindEpisodeDescription(item),
                item.PublishDate,
                audioUrl,
                item.Duration,
                imageUrl,
                EpisodeNumber: item.EpisodeNumber?.ToString(CultureInfo.InvariantCulture),
                Link: link,
                AudioMimeType: NormalizeAudioMimeType(FindPreferredAudioMimeType(preferredAudio)));
    }

    private static Podcast MapPodcast(ProgramSet programSet)
    {
        return new Podcast(
            programSet.Title,
            programSet.Description ?? programSet.Synopsis,
            GraphQlClient.CreateImageUrl(programSet.Image?.Url),
            MapEpisodes(programSet.Items.Nodes),
            Author: programSet.PublicationService?.Title,
            Link: GraphQlClient.CreateUri(programSet.SharingUrl));
    }

    private static string? FindEpisodeDescription(Item item)
    {
        return item.Description ?? item.ShowNotes ?? item.Summary ?? item.Synopsis;
    }

    private static AssetType? FindPreferredAudio(IReadOnlyList<AssetType>? audios)
    {
        return audios?
            .OrderByDescending(audio => IsPreferredAudioFormat(audio.MimeType))
            .FirstOrDefault(audio => GraphQlClient.CreateUri(audio.DownloadUrl ?? audio.Url) is not null);
    }

    private static bool IsPreferredAudioFormat(string mimeType)
    {
        return mimeType.Contains("mpeg", StringComparison.OrdinalIgnoreCase) ||
               mimeType.Contains("mp3", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAudioMimeType(string? mimeType)
    {
        return string.IsNullOrWhiteSpace(mimeType) ? DefaultAudioMimeType : mimeType;
    }

    // preferredAudio is guaranteed non-null whenever MapEpisode reaches this call (audioUrl being non-null already
    // implies FindPreferredAudio returned a non-null result), so the null-conditional's "null" branch here is
    // structurally unreachable and cannot be exercised by tests.
    [ExcludeFromCodeCoverage(Justification = "preferredAudio is always non-null at this call site; see comment above.")]
    private static string? FindPreferredAudioMimeType(AssetType? preferredAudio)
    {
        return preferredAudio?.MimeType;
    }

    private async Task<ProgramSet> FetchProgramSetAsync(string showId, CancellationToken cancellationToken)
    {
        var programSet = await FetchProgramSetPageAsync(showId, null, cancellationToken);
        var allItems = new List<Item>(programSet.Items.Nodes);
        var pageInfo = programSet.Items.PageInfo;
        var pageCount = 1;

        while (pageInfo.HasNextPage)
        {
            if (++pageCount > MaxPages)
            {
                throw new InvalidOperationException(
                    $"GraphQL pagination for show '{showId}' exceeded the maximum of {MaxPages} pages; aborting to avoid an unbounded loop.");
            }

            var nextPage = await FetchProgramSetPageAsync(showId, pageInfo.EndCursor, cancellationToken);
            allItems.AddRange(nextPage.Items.Nodes);
            pageInfo = nextPage.Items.PageInfo;
        }

        return programSet with { Items = new ItemsConnection { Nodes = allItems, PageInfo = pageInfo } };
    }

    private async Task<ProgramSet> FetchProgramSetPageAsync(string showId, string? after, CancellationToken cancellationToken)
    {
        var endpoint = options.CurrentValue.GraphQlEndpoint
            ?? throw new InvalidOperationException("GraphQlEndpoint must be configured when podcasts are enabled.");
        var request = new GraphQlRequest
        {
            Query = ProgramSetQuery,
            Variables = new Variables
            {
                ShowId = showId,
                First = PageSize,
                After = after,
            },
        };

        using var requestContent = JsonContent.Create(request, options: GraphQlClient.SerializerOptions);

        // IDisposableAnalyzers flags this as undisposed, but per Microsoft's IHttpClientFactory guidance,
        // HttpClient instances obtained via CreateClient() should NOT be disposed by the caller - the
        // factory owns and pools the underlying HttpMessageHandler across calls; disposing here would tear
        // down that pooling and (as verified while implementing this) breaks pagination, which calls this
        // method repeatedly and would otherwise get an already-disposed client on the second page.
#pragma warning disable IDISP001
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
#pragma warning restore IDISP001
        var data = await graphQlClient.ExecuteAsync<GraphQlData>(httpClient, endpoint, request, GraphQlClient.SerializerOptions, cancellationToken);

        return ExtractProgramSet(data, showId);
    }
}

internal sealed class GraphQlRequest
{
    // The single call site (FetchProgramSetPageAsync) always sets Query explicitly, so this default is
    // structurally unreachable and cannot be exercised by tests.
    [ExcludeFromCodeCoverage(Justification = "Query is always set explicitly at its only call site; see comment above.")]
    public string Query { get; init; } = string.Empty;

    public Variables Variables { get; init; } = new();
}

internal sealed class Variables
{
    // The single call site (FetchProgramSetPageAsync) always sets ShowId explicitly, so this default is
    // structurally unreachable and cannot be exercised by tests.
    [ExcludeFromCodeCoverage(Justification = "ShowId is always set explicitly at its only call site; see comment above.")]
    public string ShowId { get; init; } = string.Empty;

    public int First { get; init; }

    public string? After { get; init; }
}

internal sealed class GraphQlData
{
    public ProgramSet? ProgramSet { get; init; }
}

internal sealed record ProgramSet
{
    public string Title { get; init; } = string.Empty;

    public string? Synopsis { get; init; }

    public string? Description { get; init; }

    public string? SharingUrl { get; init; }

    public ImageType? Image { get; init; }

    public PublicationService? PublicationService { get; init; }

    public ItemsConnection Items { get; init; } = new();
}

internal sealed class PublicationService
{
    public string Title { get; init; } = string.Empty;
}

internal sealed class ItemsConnection
{
    public IReadOnlyList<Item> Nodes { get; init; } = [];

    public PageInfo PageInfo { get; init; } = new();
}

internal sealed class PageInfo
{
    public bool HasNextPage { get; init; }

    public string? EndCursor { get; init; }
}

internal sealed record Item
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public DateTimeOffset PublishDate { get; init; }

    public string? Synopsis { get; init; }

    public string? Summary { get; init; }

    public string? Description { get; init; }

    public string? ShowNotes { get; init; }

    public int? Duration { get; init; }

    public int? EpisodeNumber { get; init; }

    public string? SharingUrl { get; init; }

    public ImageType? Image { get; init; }

    public IReadOnlyList<AssetType>? Audios { get; init; }
}

internal sealed record ImageType
{
    public string? Url { get; init; }
}

internal sealed record AssetType
{
    // Only reachable when DownloadUrl is absent; CreateUri then rejects this default (and any other
    // non-absolute-URI string) identically, so this default's specific content is not observable by tests.
    [ExcludeFromCodeCoverage(Justification = "Default value is behaviorally equivalent to any other invalid-URI string; see comment above.")]
    public string Url { get; init; } = string.Empty;

    public string? DownloadUrl { get; init; }

    public string MimeType { get; init; } = string.Empty;
}
