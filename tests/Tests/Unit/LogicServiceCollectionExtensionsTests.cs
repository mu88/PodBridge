using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using PodBridge.Logic;
using PodBridge.Logic.Caching;
using PodBridge.Logic.EpisodeSourcing;
using PodBridge.Logic.Feeds;
using PodBridge.Logic.Refresh;

namespace Tests.Unit;

[TestFixture]
public class LogicServiceCollectionExtensionsTests
{
    [Test]
    public void RegisterPodBridgeServices_WithValidConfiguration_RegistersAllRequiredServices()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["PodBridge:GraphQlEndpoint"] = "https://fixture.test/graphql",
                ["PodBridge:Podcasts:0:PodcastId"] = "show1",
                ["PodBridge:Podcasts:0:ShowId"] = "show1-id",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.RegisterPodBridgeServices(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        // Assert: every service the refresh pipeline depends on must actually be resolvable,
        // otherwise the app would crash at startup - this is only exercised here because
        // integration tests replace/strip these registrations (episode source mock, no hosted services).
        provider.GetRequiredService<IEpisodeSource>().Should().NotBeNull();
        provider.GetRequiredService<IPodcastCache>().Should().NotBeNull();
        provider.GetRequiredService<TimeProvider>().Should().NotBeNull();
        provider.GetServices<IHostedService>().Should().ContainSingle(service => service is EpisodeRefreshWorker);

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IFeedUrlBuilder>().Should().NotBeNull();
    }
}
