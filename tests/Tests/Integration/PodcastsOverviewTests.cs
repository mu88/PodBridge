using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Web;
using NUnit.Framework;
using Tests.TestSupport.Builders;

namespace Tests.Integration;

[TestFixture]
[Category("Integration")]
public sealed class PodcastsOverviewTests
{
    [Test]
    public async Task GetPodcasts_WithCachedPodcasts_ReturnsAllPodcasts()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder().WithDefaults().WithTitle("Test Show").WithEpisodes(episode).Build();
        var podcastConfigs = new List<PodBridge.Logic.Config.PodcastConfig>
        {
            new PodcastConfigBuilder().WithDefaults().WithPodcastId("show-1").Build(),
            new PodcastConfigBuilder().WithDefaults().WithPodcastId("show-2").Build(),
        };
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast, podcasts: podcastConfigs);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts");

        // Assert
        response.Should().Be200Ok();
        var content = await response.Content.ReadAsStringAsync();
        var podcasts = JsonSerializer.Deserialize<JsonElement[]>(content);
        podcasts.Should().HaveCount(2);
    }

    [Test]
    public async Task GetPodcasts_WithUncachedPodcast_ShowsNotYetFetchedPlaceholder()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder().WithDefaults().WithTitle("Cached Show").WithEpisodes(episode).Build();
        var podcastConfigs = new List<PodBridge.Logic.Config.PodcastConfig>
        {
            new PodcastConfigBuilder().WithDefaults().WithPodcastId("cached-show").Build(),
            new PodcastConfigBuilder().WithDefaults().WithPodcastId("uncached-show").Build(),
        };
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            prepopulateCache: false,
            podcasts: podcastConfigs);
        using var client = factory.CreateClient();

        // Manually populate only one podcast
        var sp = factory.Services;
        var cache = (PodBridge.Logic.Caching.IPodcastCache)sp.GetService(typeof(PodBridge.Logic.Caching.IPodcastCache))!;
        cache.Update("cached-show", podcast);

        // Act
        using var response = await client.GetAsync("/api/podcasts");

        // Assert
        response.Should().Be200Ok();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Cached Show");
        content.Should().Contain("not yet fetched");
    }

    [Test]
    public async Task GetPodcasts_ReturnsCorrectFeedUrls()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts");

        // Assert
        response.Should().Be200Ok();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("/api/podcasts/test-show");
    }

    [Test]
    public async Task GetPodcasts_WithNoPodcastsConfigured_ReturnsEmptyArray()
    {
        // Arrange
        await using var factory = new TestWebApplicationFactory(podcasts: []);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts");

        // Assert
        response.Should().Be200Ok();
        var content = await response.Content.ReadAsStringAsync();
        var podcasts = JsonSerializer.Deserialize<JsonElement[]>(content);
        podcasts.Should().BeEmpty();
    }
}
