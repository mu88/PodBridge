using Microsoft.Extensions.Diagnostics.HealthChecks;
using PodBridge.Logic.Refresh;

namespace PodBridge.Api.Refresh;

/// <summary>
/// Reports <see cref="HealthStatus.Degraded"/> (not <see cref="HealthStatus.Unhealthy"/>) when the last refresh
/// attempt failed for at least one podcast: PodBridge keeps serving previously cached feeds in that case, so the
/// app itself is still fully functional - only the underlying data may be stale. Using Degraded instead of
/// Unhealthy avoids triggering container restarts/load-balancer removal for a transient upstream GraphQL issue.
/// </summary>
internal sealed class EpisodeRefreshHealthCheck(IEpisodeRefreshHealthState healthState) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var failingPodcastIds = healthState.GetFailingPodcastIds();
        var result = failingPodcastIds.Count == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded($"Episode refresh failed for: {string.Join(", ", failingPodcastIds)}");

        return Task.FromResult(result);
    }
}
