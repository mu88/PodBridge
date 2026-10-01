using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PodBridge.Logic.Caching;
using PodBridge.Logic.Config;
using PodBridge.Logic.EpisodeSourcing;
using PodBridge.Logic.Feeds;
using PodBridge.Logic.PodcastManagement;
using PodBridge.Logic.Refresh;
using PodBridge.Logic.Versioning;

namespace PodBridge.Logic;

public static class LogicServiceCollectionExtensions
{
    public static IServiceCollection RegisterPodBridgeServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<PodBridgeOptions>()
            .Bind(configuration.GetSection(PodBridgeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The named client below has no client-specific configuration (no BaseAddress/handlers/timeout),
        // so IHttpClientFactory.CreateClient(HttpClientName) behaves identically whether registered here or
        // not - equivalent mutant. Kept for explicitness/future extensibility (e.g. adding a Polly handler).
        // Stryker disable once all: unconfigured named client is behaviorally identical to an unregistered one
        services.AddHttpClient(GraphQlEpisodeSource.HttpClientName);
        services.AddSingleton<IEpisodeSource, GraphQlEpisodeSource>();
        services.AddHttpClient<IPodcastDirectory, GraphQlPodcastDirectory>();
        services.AddSingleton<GraphQlClient>();
        services.AddSingleton<IPodcastCache, PodcastCache>();
        services.AddScoped<IFeedUrlBuilder, FeedUrlBuilder>();
        services.AddSingleton<IAppVersionProvider, AppVersionProvider>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEpisodeRefreshHealthState, EpisodeRefreshHealthState>();
        services.AddSingleton<IPodcastRefreshService, PodcastRefreshService>();
        services.AddScoped<IPodcastManagementService, PodcastManagementService>();
        services.AddHostedService<EpisodeRefreshWorker>();

        return services;
    }
}

