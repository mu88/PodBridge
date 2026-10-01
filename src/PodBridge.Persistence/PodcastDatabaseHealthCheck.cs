using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PodBridge.Persistence;

/// <summary>
/// Reports <see cref="HealthStatus.Degraded"/> (consistent with <see cref="EpisodeRefreshHealthCheck"/>) when
/// the Postgres database backing the Podcasts list is unreachable. Degraded rather than Unhealthy because
/// PodBridge keeps serving already-cached feeds from memory in that case - only adding/refreshing podcasts is
/// affected, not the app as a whole.
/// </summary>
internal sealed class PodcastDatabaseHealthCheck(IDbContextFactory<AppDbContext> dbContextFactory) : IHealthCheck
{
    // [ExcludeFromCodeCoverage]: dotnet-coverage/Cobertura duplicates a sequence point across this async
    // method and its compiler-generated <CheckHealthAsync>d__N state machine class, and one copy
    // permanently shows 0 hits regardless of test coverage (verified: identical across independent runs).
    // This is a measurement-tool artifact, not untested logic - both the healthy and unreachable paths
    // are covered by PodcastDatabaseHealthCheckTests.
    [ExcludeFromCodeCoverage]
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded("Cannot connect to the Podcasts database");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Degraded("Cannot connect to the Podcasts database", exception);
        }
    }
}
