using Microsoft.EntityFrameworkCore;
using PodBridge.Logic.Config;
using PodBridge.Logic.Shared;

namespace PodBridge.Persistence;

internal sealed class PodcastRepository(IDbContextFactory<AppDbContext> dbContextFactory) : IPodcastRepository
{
    public async Task<IReadOnlyList<PodcastConfig>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Podcasts.ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsWithPodcastIdAsync(string podcastId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Podcasts.AnyAsync(podcast => podcast.PodcastId == podcastId, cancellationToken);
    }

    public async Task<PodcastRepositoryAddOutcome> AddAsync(PodcastConfig podcast, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Podcasts.Add(podcast);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            return IsShowIdConflict(exception) ? PodcastRepositoryAddOutcome.ShowIdAlreadyExists : PodcastRepositoryAddOutcome.Failed;
        }

        return PodcastRepositoryAddOutcome.Added;
    }

    // Provider-agnostic detection of the specific "ShowId already exists" conflict, without coupling this
    // repository to a specific ADO.NET provider's exception type: Npgsql surfaces the unique index name
    // ("IX_Podcasts_ShowId") in its exception message, SQLite (used in tests) surfaces the table/column
    // pair ("Podcasts.ShowId") - checking for "ShowId" covers both. Any other DbUpdateException (e.g. a
    // rare PodcastId slug race, or no InnerException at all) is reported as a generic failure instead of
    // misleadingly claiming the show was already added.
    private static bool IsShowIdConflict(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("ShowId", StringComparison.Ordinal) == true;
}
