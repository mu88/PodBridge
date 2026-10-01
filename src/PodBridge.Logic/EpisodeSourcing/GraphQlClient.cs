using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace PodBridge.Logic.EpisodeSourcing;

/// <summary>
/// Shared GraphQL transport, composed into <see cref="GraphQlEpisodeSource"/> and <see cref="GraphQlPodcastDirectory"/>
/// as a collaborator (rather than a common base class): posts a request body, deserializes the generic
/// Data/Errors envelope, and throws on GraphQL-level errors. Callers keep their own HttpClient acquisition
/// (typed vs. named client) and business-specific error/empty handling (e.g. "no program set was returned").
/// Instance (not static) methods so tests can substitute this collaborator via NSubstitute.
/// </summary>
internal sealed class GraphQlClient
{
    /// <summary>Shared camelCase options for both requests and responses across all GraphQL callers.</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private const int ImageWidth = 640;

    /// <summary>Resolves the image URL's <c>{width}</c> placeholder to <see cref="ImageWidth"/> and parses the result.</summary>
    public static Uri? CreateImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var width = ImageWidth.ToString(CultureInfo.InvariantCulture);
        return CreateUri(url.Replace("{width}", width, StringComparison.Ordinal));
    }

    public static Uri? CreateUri(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Kept as an instance method so tests can substitute this collaborator via NSubstitute; see class summary.")]
    [SuppressMessage("Maintainability", "S2325:Methods and properties that don't access instance data should be static", Justification = "Kept as an instance method so tests can substitute this collaborator via NSubstitute; see class summary.")]
    public async Task<TData?> ExecuteAsync<TData>(
        HttpClient httpClient,
        Uri endpoint,
        object requestBody,
        JsonSerializerOptions serializerOptions,
        CancellationToken cancellationToken)
    {
        using var requestContent = JsonContent.Create(requestBody, options: serializerOptions);
        using var response = await httpClient.PostAsync(endpoint, requestContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<GraphQlResponse<TData>>(contentStream, serializerOptions, cancellationToken);

        if (payload?.Errors is { Count: > 0 })
        {
            throw new InvalidOperationException(string.Join("; ", payload.Errors.Select(error => error.Message)));
        }

        return payload is null ? default : payload.Data;
    }
}

internal sealed class GraphQlResponse<TData>
{
    public TData? Data { get; init; }

    public IReadOnlyList<GraphQlError>? Errors { get; init; }
}

internal sealed class GraphQlError
{
    public string Message { get; init; } = string.Empty;
}
