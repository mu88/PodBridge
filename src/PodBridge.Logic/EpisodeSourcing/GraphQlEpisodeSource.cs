using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PodBridge.Logic.Config;
using PodBridge.Logic.Domain;

namespace PodBridge.Logic.EpisodeSourcing;

internal sealed class GraphQlEpisodeSource(HttpClient httpClient, IOptions<PodBridgeOptions> options) : IEpisodeSource
{
    private const int ImageWidth = 640;
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

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<Podcast> FetchEpisodesAsync(PodcastConfig podcast, CancellationToken cancellationToken)
    {
        using var activity = Observability.Source.StartActivity();
        activity?.SetTag(Observability.PodcastIdTag, podcast.PodcastId);

        var programSet = await FetchProgramSetAsync(podcast.ShowId, cancellationToken);
        return MapPodcast(programSet);
    }

    private static ProgramSet ExtractProgramSet(GraphQlResponse? payload, string showId)
    {
        if (payload?.Errors is { Count: > 0 })
        {
            throw new InvalidOperationException(string.Join("; ", payload.Errors.Select(error => error.Message)));
        }

        return payload?.Data?.ProgramSet
            ?? throw new InvalidOperationException($"No program set was returned for show '{showId}'.");
    }

    private static IReadOnlyList<Episode> MapEpisodes(IReadOnlyList<Item> items)
    {
        return items
            .Select(MapEpisode)
            .OfType<Episode>()
            .ToList();
    }

    private static Episode? MapEpisode(Item item)
    {
        var preferredAudio = GetPreferredAudio(item.Audios);
        var audioUrl = CreateUri(preferredAudio?.DownloadUrl ?? preferredAudio?.Url);
        var imageUrl = CreateImageUrl(item.Image?.Url);
        var link = CreateUri(item.SharingUrl);

        return audioUrl is null
            ? null
            : new Episode(
                item.Id,
                item.Title,
                GetEpisodeDescription(item),
                item.PublishDate,
                audioUrl,
                item.Duration,
                imageUrl,
                EpisodeNumber: item.EpisodeNumber?.ToString(CultureInfo.InvariantCulture),
                Link: link,
                AudioMimeType: NormalizeAudioMimeType(GetPreferredAudioMimeType(preferredAudio)));
    }

    private static Podcast MapPodcast(ProgramSet programSet)
    {
        return new Podcast(
            programSet.Title,
            programSet.Description ?? programSet.Synopsis,
            CreateImageUrl(programSet.Image?.Url),
            MapEpisodes(programSet.Items.Nodes),
            Author: programSet.PublicationService?.Title,
            Link: CreateUri(programSet.SharingUrl));
    }

    private static string? GetEpisodeDescription(Item item)
    {
        return item.Description ?? item.ShowNotes ?? item.Summary ?? item.Synopsis;
    }

    private static AssetType? GetPreferredAudio(IReadOnlyList<AssetType>? audios)
    {
        return audios?
            .OrderByDescending(audio => IsPreferredAudioFormat(audio.MimeType))
            .FirstOrDefault(audio => CreateUri(audio.DownloadUrl ?? audio.Url) is not null);
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
    // implies GetPreferredAudio returned a non-null result), so the null-conditional's "null" branch here is
    // structurally unreachable and cannot be exercised by tests.
    [ExcludeFromCodeCoverage(Justification = "preferredAudio is always non-null at this call site; see comment above.")]
    private static string? GetPreferredAudioMimeType(AssetType? preferredAudio)
    {
        return preferredAudio?.MimeType;
    }

    private static Uri? CreateImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var width = ImageWidth.ToString(CultureInfo.InvariantCulture);
        return CreateUri(url.Replace("{width}", width, StringComparison.Ordinal));
    }

    private static Uri? CreateUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
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
        var endpoint = options.Value.GraphQlEndpoint
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

        using var requestContent = JsonContent.Create(request, options: SerializerOptions);
        using var response = await httpClient.PostAsync(endpoint, requestContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<GraphQlResponse>(contentStream, SerializerOptions, cancellationToken);

        return ExtractProgramSet(payload, showId);
    }
}

internal sealed class GraphQlRequest
{
    public string Query { get; init; } = string.Empty;

    public Variables Variables { get; init; } = new();
}

internal sealed class Variables
{
    public string ShowId { get; init; } = string.Empty;

    public int First { get; init; }

    public string? After { get; init; }
}

internal sealed class GraphQlResponse
{
    public GraphQlData? Data { get; init; }

    public IReadOnlyList<GraphQlError>? Errors { get; init; }
}

internal sealed class GraphQlData
{
    public ProgramSet? ProgramSet { get; init; }
}

internal sealed class GraphQlError
{
    public string Message { get; init; } = string.Empty;
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
    public string Url { get; init; } = string.Empty;

    public string? DownloadUrl { get; init; }

    public string MimeType { get; init; } = string.Empty;
}
