using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using PodBridge.Logic;
using PodBridge.Logic.Caching;
using PodBridge.Logic.EpisodeSourcing;
using PodBridge.Logic.Feeds;
using PodBridge.Logic.Refresh;
using PodBridge.Logic.Versioning;
using PodBridge.Persistence;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
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
                ["ConnectionStrings:PodBridgeDb"] = "Host=localhost;Database=podbridge;Username=test;Password=test",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.RegisterPodBridgeServices(configuration);
        services.RegisterPodBridgePersistenceServices(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        // Assert: every service the refresh pipeline depends on must actually be resolvable,
        // otherwise the app would crash at startup - this is only exercised here because
        // integration tests replace/strip these registrations (episode source mock, no hosted services).
        provider.GetRequiredService<IEpisodeSource>().Should().NotBeNull();
        provider.GetRequiredService<IPodcastDirectory>().Should().NotBeNull();
        provider.GetRequiredService<IPodcastCache>().Should().NotBeNull();
        provider.GetRequiredService<TimeProvider>().Should().NotBeNull();
        provider.GetRequiredService<IAppVersionProvider>().Should().NotBeNull();
        provider.GetServices<IHostedService>().Should().ContainSingle(service => service is EpisodeRefreshWorker);

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IFeedUrlBuilder>().Should().NotBeNull();
    }

    [Test]
    public void RegisterPodBridgeServices_WithValidConfiguration_RegistersPodcastDatabaseHealthCheckByName()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["PodBridge:GraphQlEndpoint"] = "https://fixture.test/graphql",
                ["ConnectionStrings:PodBridgeDb"] = "Host=localhost;Database=podbridge;Username=test;******",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.RegisterPodBridgeServices(configuration);
        services.RegisterPodBridgePersistenceServices(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        // Assert - name must be exactly "podcast-database", otherwise a health check dashboard/monitoring
        // config referencing it by string would silently stop matching. The "episode-refresh" health check
        // is registered separately in PodBridge.Api's Program.cs (it depends on IHealthCheck, an ASP.NET
        // Core hosting concern, so it lives in Api rather than here) and is covered by HealthCheckEndpointTests.
        var healthCheckOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        healthCheckOptions.Registrations.Select(registration => registration.Name)
            .Should().BeEquivalentTo(["podcast-database"]);
    }
}
