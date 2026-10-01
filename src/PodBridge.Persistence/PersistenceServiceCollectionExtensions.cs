using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PodBridge.Logic.Shared;

namespace PodBridge.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection RegisterPodBridgePersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Reads the actual host/port/user/password/database via a level of indirection (see
        // PostgresConnectionStringResolver) so no hosting-platform-specific env var names ever appear in
        // code; falls back to the conventional "ConnectionStrings:PodBridgeDb" single connection string
        // (e.g. for local development) if that indirection isn't configured.
        services.AddDbContextFactory<AppDbContext>(dbContextOptions => dbContextOptions
            .UseNpgsql(
                PostgresConnectionStringResolver.Resolve(configuration),
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure())

            // All reads across PodBridge are simple read-only queries (writes always go through explicit
            // Add/AddRange, which track regardless of this default) - NoTracking as the app-wide default
            // keeps every read fast/allocation-light without callers having to remember .AsNoTracking().
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddSingleton<IPodcastRepository, PodcastRepository>();

        services.AddHealthChecks()
            .AddCheck<PodcastDatabaseHealthCheck>("podcast-database");

        return services;
    }
}
