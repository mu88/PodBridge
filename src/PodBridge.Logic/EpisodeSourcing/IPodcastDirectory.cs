namespace PodBridge.Logic.EpisodeSourcing;

/// <summary>
/// Looks up shows by title and fetches lightweight metadata previews, backing the "Add Podcast" self-service
/// flow. Deliberately separate from <see cref="IEpisodeSource"/>: that interface always does a full,
/// potentially expensive episode backfill, while this one is used interactively from a request handler and
/// must stay cheap.
/// </summary>
public interface IPodcastDirectory
{
    Task<IReadOnlyList<PodcastSearchResult>> SearchAsync(string query, CancellationToken cancellationToken);

    Task<PodcastPreview?> FindPreviewAsync(string showId, CancellationToken cancellationToken);
}
