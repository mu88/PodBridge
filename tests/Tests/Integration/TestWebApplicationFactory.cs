using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using PodBridge.Logic;
using PodBridge.Logic.Caching;
using PodBridge.Logic.Config;
using PodBridge.Logic.EpisodeSourcing;
using PodBridge.Logic.PodcastManagement;
using PodBridge.Logic.Security;
using PodBridge.Logic.Shared;
using PodBridge.Persistence;
using Tests.TestSupport;
using Tests.TestSupport.Builders;

namespace Tests.Integration;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteAppDbContextFactoryScope _dbContextScope;
    private readonly Podcast? _testPodcast;
    private readonly bool _prepopulateCache;
    private readonly IReadOnlyList<PodcastConfig> _podcasts;
    private readonly IPodcastDirectory? _podcastDirectory;
    private readonly IPodcastManagementService? _podcastManagementService;
    private readonly int? _rateLimitingPermitLimit;
    private readonly int? _rateLimitingWindowMinutes;
    private readonly bool _authEnabled;
    private readonly string? _authUsername;
    private readonly string? _authPassword;
    private readonly int? _authRateLimitingPermitLimit;
    private readonly int? _authRateLimitingWindowMinutes;

    public TestWebApplicationFactory(
        Podcast? testPodcast = null,
        bool prepopulateCache = true,
        IReadOnlyList<PodcastConfig>? podcasts = null,
        IPodcastDirectory? podcastDirectory = null,
        IPodcastManagementService? podcastManagementService = null,
        int? rateLimitingPermitLimit = null,
        int? rateLimitingWindowMinutes = null,
        bool authEnabled = false,
        string? authUsername = null,
        string? authPassword = null,
        int? authRateLimitingPermitLimit = null,
        int? authRateLimitingWindowMinutes = null)
    {
        _testPodcast = testPodcast;
        _prepopulateCache = prepopulateCache;
        _podcasts = podcasts ?? [new PodcastConfigBuilder().WithDefaults().WithPodcastId("test-show").WithShowId("test-show-id").Build()];
        _podcastDirectory = podcastDirectory;
        _podcastManagementService = podcastManagementService;
        _rateLimitingPermitLimit = rateLimitingPermitLimit;
        _rateLimitingWindowMinutes = rateLimitingWindowMinutes;
        _authEnabled = authEnabled;
        _authUsername = authUsername;
        _authPassword = authPassword;
        _authRateLimitingPermitLimit = authRateLimitingPermitLimit;
        _authRateLimitingWindowMinutes = authRateLimitingWindowMinutes;
        _dbContextScope = new SqliteAppDbContextFactoryScope(_podcasts);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((context, config) => config.AddInMemoryCollection(BuildConfigurationSettings()));

        builder.ConfigureServices(services =>
        {
            RemoveHostedServices(services);
            ReplaceEpisodeSource(services);
            ReplaceAppDbContextFactory(services);
            ReplacePodcastDirectory(services);
            ReplacePodcastManagementService(services);
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _dbContextScope.Dispose();
        }
    }

    private static void RemoveHostedServices(IServiceCollection services)
    {
        var hostedServiceDescriptors = services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToList();
        foreach (var descriptor in hostedServiceDescriptors)
        {
            services.Remove(descriptor);
        }
    }

    private void ReplaceEpisodeSource(IServiceCollection services)
    {
        services.RemoveAll<IEpisodeSource>();

        var mockEpisodeSource = Substitute.For<IEpisodeSource>();
        if (_testPodcast is not null)
        {
            mockEpisodeSource.FetchEpisodesAsync(Arg.Any<PodcastConfig>(), Arg.Any<CancellationToken>())
                .Returns(_testPodcast);
        }

        services.AddSingleton<IEpisodeSource>(_ => mockEpisodeSource);
    }

    private void ReplacePodcastDirectory(IServiceCollection services)
    {
        if (_podcastDirectory is null)
        {
            return;
        }

        services.RemoveAll<IPodcastDirectory>();
        services.AddSingleton(_podcastDirectory);
    }

    private void ReplacePodcastManagementService(IServiceCollection services)
    {
        if (_podcastManagementService is null)
        {
            return;
        }

        services.RemoveAll<IPodcastManagementService>();
        services.AddSingleton(_podcastManagementService);
    }

    private void ReplaceAppDbContextFactory(IServiceCollection services)
    {
        services.RemoveAll<AppDbContext>();
        services.RemoveAll<DbContextOptions>();
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<IDbContextFactory<AppDbContext>>();

        services.AddSingleton(_dbContextScope.DbContextFactory);
    }

    private Dictionary<string, string?> BuildConfigurationSettings()
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            { "ASPNETCORE_ENVIRONMENT", "Testing" },
            { "PodBridge:RefreshInterval", "01:00:00" },
            { "PodBridge:GraphQlEndpoint", "https://fixture.test/graphql" },
            { "PodBridge:BackgroundRefreshEnabled", "false" },
        };

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
                // Hashed here (rather than accepting a pre-hashed value) so callers can keep passing the
                // plaintext credentials a real client would authenticate with - matching CredentialHasher's
                // production usage in BasicAuthenticationHandler.
                settings.Add("PodBridge:Auth:UsernameHash", CredentialHasher.Hash(_authUsername));
            }

            if (!string.IsNullOrWhiteSpace(_authPassword))
            {
                settings.Add("PodBridge:Auth:PasswordHash", CredentialHasher.Hash(_authPassword));
            }

            if (_authRateLimitingPermitLimit is not null)
            {
                settings.Add("PodBridge:Auth:RateLimitingPermitLimit", _authRateLimitingPermitLimit.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (_authRateLimitingWindowMinutes is not null)
            {
                settings.Add("PodBridge:Auth:RateLimitingWindowMinutes", _authRateLimitingWindowMinutes.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        return settings;
    }

    private sealed class NoOpHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
