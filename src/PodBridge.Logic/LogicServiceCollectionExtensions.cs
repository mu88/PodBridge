using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PodBridge.Logic.Caching;
using PodBridge.Logic.Config;
using PodBridge.Logic.EpisodeSourcing;
using PodBridge.Logic.Feeds;
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
        services.AddHttpClient<IEpisodeSource, GraphQlEpisodeSource>();
        services.AddSingleton<IPodcastCache, PodcastCache>();
        services.AddScoped<IFeedUrlBuilder, FeedUrlBuilder>();
        services.AddSingleton<IAppVersionProvider, AppVersionProvider>();
        services.AddSingleton(TimeProvider.System);
        services.AddHostedService<EpisodeRefreshWorker>();

        return services;
    }
}
