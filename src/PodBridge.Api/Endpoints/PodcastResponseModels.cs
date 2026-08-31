namespace PodBridge.Api.Endpoints;

internal sealed record PodcastSummaryResponse
{
    required public string PodcastId { get; init; }
    required public string Title { get; init; }
    required public string FeedUrl { get; init; }
}

internal sealed record PodcastDetailsResponse
{
    required public string PodcastId { get; init; }
    required public string Title { get; init; }
    public string? Description { get; init; }
    public string? Author { get; init; }
    public string? Language { get; init; }
    public Uri? ImageUrl { get; init; }
    public Uri? Link { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
    required public string FeedUrl { get; init; }
    required public IReadOnlyList<PodcastEpisodeResponse> Episodes { get; init; }
}

internal sealed record PodcastEpisodeResponse
{
    required public string Guid { get; init; }
    required public string Title { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset PublishDate { get; init; }
    required public Uri AudioUrl { get; init; }
    public int? DurationSeconds { get; init; }
    public Uri? ImageUrl { get; init; }
    public string? EpisodeNumber { get; init; }
    public Uri? Link { get; init; }
}
