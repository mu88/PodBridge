using PodBridge.Logic.Config;

namespace PodBridge.Logic.Shared;

/// <summary>
/// Abstraction over the Podcasts persistence store, so that consumers (e.g. <c>EpisodeRefreshWorker</c> and
/// <c>PodcastManagementService</c>) depend only on this contract rather than on the concrete EF Core
/// <c>AppDbContext</c>/<c>PodBridge.Persistence</c> project - keeping the Dependency Rule intact (Logic must
/// not depend on infrastructure). Lives in <c>Shared</c> because it is now used by more than one feature.
/// </summary>
public interface IPodcastRepository
{
    Task<IReadOnlyList<PodcastConfig>> GetAllAsync(CancellationToken cancellationToken);

    Task<bool> ExistsWithPodcastIdAsync(string podcastId, CancellationToken cancellationToken);

    Task<PodcastRepositoryAddOutcome> AddAsync(PodcastConfig podcast, CancellationToken cancellationToken);
}

public enum PodcastRepositoryAddOutcome
{
    Added,
    ShowIdAlreadyExists,
    Failed,
}
