using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using PodBridge.Logic;
using PodBridge.Logic.Caching;
using PodBridge.Logic.Config;
using PodBridge.Logic.Domain;
using PodBridge.Logic.EpisodeSourcing;
using Tests.TestSupport.Builders;

namespace Tests.Integration;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Podcast? _testPodcast;
    private readonly bool _prepopulateCache;
    private readonly IReadOnlyList<PodcastConfig> _podcasts;
    private readonly int? _rateLimitingPermitLimit;
    private readonly int? _rateLimitingWindowMinutes;
    private readonly bool _authEnabled;
    private readonly string? _authUsername;
    private readonly string? _authPassword;
    private readonly string? _pathBase;

    public TestWebApplicationFactory(
        Podcast? testPodcast = null,
        bool prepopulateCache = true,
        IReadOnlyList<PodcastConfig>? podcasts = null,
        int? rateLimitingPermitLimit = null,
        int? rateLimitingWindowMinutes = null,
        bool authEnabled = false,
        string? authUsername = null,
        string? authPassword = null,
        string? pathBase = null)
    {
        _testPodcast = testPodcast;
        _prepopulateCache = prepopulateCache;
        _podcasts = podcasts ?? [new PodcastConfigBuilder().WithDefaults().WithPodcastId("test-show").WithShowId("test-show-id").Build()];
        _rateLimitingPermitLimit = rateLimitingPermitLimit;
        _rateLimitingWindowMinutes = rateLimitingWindowMinutes;
        _authEnabled = authEnabled;
        _authUsername = authUsername;
        _authPassword = authPassword;
        _pathBase = pathBase;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) => config.AddInMemoryCollection(BuildConfigurationSettings()));

        builder.ConfigureServices(services =>
        {
            var hostedServiceDescriptors = services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToList();
            foreach (var descriptor in hostedServiceDescriptors)
            {
                services.Remove(descriptor);
            }

            var episodeSourceDescriptors = services.Where(descriptor =>
                descriptor.ServiceType == typeof(IEpisodeSource) ||
                descriptor.ServiceType.Name.Contains("GraphQlEpisodeSource", StringComparison.Ordinal)).ToList();
            foreach (var descriptor in episodeSourceDescriptors)
            {
                services.Remove(descriptor);
            }

            var mockEpisodeSource = Substitute.For<IEpisodeSource>();
            if (_testPodcast != null)
            {
                mockEpisodeSource.FetchEpisodesAsync(Arg.Any<PodcastConfig>(), Arg.Any<CancellationToken>())
                    .Returns(_testPodcast);
            }

            services.AddSingleton<IEpisodeSource>(_ => mockEpisodeSource);
        });

        if (_prepopulateCache && _testPodcast != null)
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IHostedService>(sp =>
                {
                    var cache = sp.GetRequiredService<IPodcastCache>();
                    foreach (var podcast in _podcasts)
                    {
                        cache.Update(podcast.PodcastId, _testPodcast);
                    }

                    return new NoOpHostedService();
                });
            });
        }
    }

    private Dictionary<string, string?> BuildConfigurationSettings()
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "ASPNETCORE_ENVIRONMENT", "Testing" },
            { "PodBridge:RefreshIntervalMinutes", "60" },
            { "PodBridge:GraphQlEndpoint", "https://fixture.test/graphql" },
        };

        if (!string.IsNullOrWhiteSpace(_pathBase))
        {
            settings.Add("PodBridge:PathBase", _pathBase);
        }

        if (_rateLimitingPermitLimit is not null)
        {
            settings.Add("PodBridge:RateLimitingPermitLimit", _rateLimitingPermitLimit.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (_rateLimitingWindowMinutes is not null)
        {
            settings.Add("PodBridge:RateLimitingWindowMinutes", _rateLimitingWindowMinutes.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (_authEnabled)
        {
            settings.Add("PodBridge:Auth:Enabled", "true");
            if (!string.IsNullOrWhiteSpace(_authUsername))
            {
                settings.Add("PodBridge:Auth:Username", _authUsername);
            }

            if (!string.IsNullOrWhiteSpace(_authPassword))
            {
                settings.Add("PodBridge:Auth:Password", _authPassword);
            }
        }

        AddPodcastSettings(settings);
        return settings;
    }

    private void AddPodcastSettings(Dictionary<string, string?> settings)
    {
        for (var i = 0; i < _podcasts.Count; i++)
        {
            var podcast = _podcasts[i];
            settings.Add($"PodBridge:Podcasts:{i.ToString(CultureInfo.InvariantCulture)}:PodcastId", podcast.PodcastId);
            settings.Add($"PodBridge:Podcasts:{i.ToString(CultureInfo.InvariantCulture)}:ShowId", podcast.ShowId);
        }
    }

    private sealed class NoOpHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
