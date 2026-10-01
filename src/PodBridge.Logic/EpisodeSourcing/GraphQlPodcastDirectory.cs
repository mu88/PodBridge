using Microsoft.Extensions.Options;
using PodBridge.Logic.Config;

namespace PodBridge.Logic.EpisodeSourcing;

internal sealed class GraphQlPodcastDirectory(HttpClient httpClient, IOptionsMonitor<PodBridgeOptions> options, GraphQlClient graphQlClient) : IPodcastDirectory
{
    private const int SearchLimit = 10;

    private const string SearchQuery = """
        query($query: String!, $limit: Int!) {
          search(query: $query, type: ProgramSets, limit: $limit) {
            programSets {
              nodes {
                id
                title
                numberOfElements
              }
            }
          }
        }
        """;

    private const string PreviewQuery = """
        query($showId: ID!) {
          programSet(id: $showId) {
            title
            image {
              url
            }
          }
        }
        """;

    public async Task<IReadOnlyList<PodcastSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var payload = await ExecuteAsync<SearchData>(
            SearchQuery,
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["query"] = query, ["limit"] = SearchLimit },
            cancellationToken);

        var nodes = payload?.Search?.ProgramSets?.Nodes ?? [];
        return nodes
            .Select(node => new PodcastSearchResult(node.Id, node.Title, node.NumberOfElements))
            .ToList();
    }

    public async Task<PodcastPreview?> FindPreviewAsync(string showId, CancellationToken cancellationToken)
    {
        var payload = await ExecuteAsync<PreviewData>(
            PreviewQuery,
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["showId"] = showId },
            cancellationToken);

        var programSet = payload?.ProgramSet;
        return programSet is null ? null : new PodcastPreview(programSet.Title, GraphQlClient.CreateImageUrl(programSet.Image?.Url));
    }

    private async Task<TData?> ExecuteAsync<TData>(string query, IReadOnlyDictionary<string, object?> variables, CancellationToken cancellationToken)
    {
        var endpoint = options.CurrentValue.GraphQlEndpoint
            ?? throw new InvalidOperationException("GraphQlEndpoint must be configured to search for or add podcasts.");

        return await graphQlClient.ExecuteAsync<TData>(httpClient, endpoint, new { query, variables }, GraphQlClient.SerializerOptions, cancellationToken);
    }
}

internal sealed class SearchData
{
    public SearchResult? Search { get; init; }
}

internal sealed class SearchResult
{
    public ProgramSetsConnection? ProgramSets { get; init; }
}

internal sealed class ProgramSetsConnection
{
    public IReadOnlyList<ProgramSetNode> Nodes { get; init; } = [];
}

internal sealed class ProgramSetNode
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public int NumberOfElements { get; init; }
}

internal sealed class PreviewData
{
    public PreviewProgramSet? ProgramSet { get; init; }
}

internal sealed class PreviewProgramSet
{
    public string Title { get; init; } = string.Empty;

    public PreviewImage? Image { get; init; }
}

internal sealed class PreviewImage
{
    public string? Url { get; init; }
}
