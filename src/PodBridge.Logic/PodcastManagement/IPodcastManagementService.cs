namespace PodBridge.Logic.PodcastManagement;

/// <summary>
/// Encapsulates the "Add Podcast" write workflow (unique PodcastId/slug generation, persistence, conflict
/// detection and triggering the initial episode backfill) so it can be reused by more than one caller - the
/// Blazor "Add Podcast" page and the JSON API endpoints - instead of being duplicated or UI-only.
/// </summary>
public interface IPodcastManagementService
{
    /// <summary>
    /// Adds a podcast whose title is already known (e.g. from a prior <see cref="EpisodeSourcing.IPodcastDirectory.SearchAsync"/> result),
    /// avoiding a redundant directory lookup.
    /// </summary>
    Task<PodcastAddResult> AddAsync(string showId, string title, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the show's title via the podcast directory first, then adds it.
    /// </summary>
    Task<PodcastAddResult> AddByShowIdAsync(string showId, CancellationToken cancellationToken);
}

public enum PodcastAddStatus
{
    Added,
    ShowIdAlreadyExists,
    ShowIdNotFound,
    Failed,
}

public sealed record PodcastAddResult(PodcastAddStatus Status, string? Title = null, string? PodcastId = null);
