using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using FluentAssertions.Web;
using NUnit.Framework;
using Tests.TestSupport.Builders;

namespace Tests.Integration;

[TestFixture]
public sealed class PodcastDetailEndpointTests
{
    [Test]
    public async Task GetPodcast_WithCachedPodcast_ReturnsRssFeedByDefault()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build() with { Title = "Test Episode" };
        var podcast = new PodcastBuilder().WithDefaults().WithTitle("Test Podcast").WithEpisodes(episode).Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts/test-show");

        // Assert
        response.Should().Be200Ok();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/rss+xml");
        var content = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(content);
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("rss");
        var channel = doc.Root.Element("channel");
        channel.Should().NotBeNull();
        channel!.Element("title")!.Value.Should().Be("Test Podcast");
    }

    [Test]
    public async Task GetPodcast_WithUncachedPodcast_Returns503()
    {
        // Arrange
        await using var factory = new TestWebApplicationFactory(prepopulateCache: false);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts/test-show");

        // Assert
        response.Should().Be503ServiceUnavailable();
    }

    [Test]
    public async Task GetPodcast_WithNonExistentPodcastId_Returns404()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts/non-existent-show");

        // Assert
        response.Should().Be404NotFound();
    }

    [Test]
    public async Task GetPodcast_IncludesAtomSelfLink()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts/test-show");

        // Assert
        response.Should().Be200Ok();
        var content = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(content);
        var atomNamespace = XNamespace.Get("http://www.w3.org/2005/Atom");
        var atomLink = doc.Descendants(atomNamespace + "link").FirstOrDefault();
        atomLink.Should().NotBeNull();
        atomLink!.Attribute("rel")!.Value.Should().Be("self");
        atomLink.Attribute("href")!.Value.Should().Contain("/api/podcasts/test-show");
    }

    [Test]
    public async Task GetPodcast_IncludesAllEpisodes()
    {
        // Arrange
        var episode1 = new EpisodeBuilder().WithDefaults().Build() with { Title = "Episode 1" };
        var episode2 = new EpisodeBuilder().WithDefaults().Build() with { Title = "Episode 2" };
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode1, episode2).Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts/test-show");

        // Assert
        response.Should().Be200Ok();
        var content = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(content);
        var items = doc.Descendants("item").ToList();
        items.Should().HaveCount(2);
    }

    [Test]
    public async Task GetPodcast_WithFormatJson_ReturnsJsonWithFullEpisodeDetails()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build() with { Title = "Test Episode", Description = "A detailed description" };
        var podcast = new PodcastBuilder().WithDefaults().WithTitle("Test Podcast").WithEpisodes(episode).Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts/test-show?format=json");

        // Assert
        response.Should().Be200Ok();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("podcastId").GetString().Should().Be("test-show");
        json.RootElement.GetProperty("title").GetString().Should().Be("Test Podcast");
        json.RootElement.GetProperty("feedUrl").GetString().Should().Contain("/api/podcasts/test-show");
        var episodes = json.RootElement.GetProperty("episodes");
        episodes.GetArrayLength().Should().Be(1);
        var firstEpisode = episodes[0];
        firstEpisode.GetProperty("title").GetString().Should().Be("Test Episode");
        firstEpisode.GetProperty("description").GetString().Should().Be("A detailed description");
        firstEpisode.TryGetProperty("audioUrl", out _).Should().BeTrue();
        firstEpisode.TryGetProperty("publishDate", out _).Should().BeTrue();
        firstEpisode.TryGetProperty("durationSeconds", out _).Should().BeTrue();
        firstEpisode.TryGetProperty("imageUrl", out _).Should().BeTrue();
    }

    [Test]
    public async Task GetPodcast_WithFormatJsonCaseInsensitive_ReturnsJson()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts/test-show?format=JSON");

        // Assert
        response.Should().Be200Ok();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Test]
    public async Task GetPodcast_WithUnknownFormat_ReturnsRssFeed()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts/test-show?format=yaml");

        // Assert
        response.Should().Be200Ok();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/rss+xml");
    }

    [Test]
    public async Task GetPodcast_WithFormatJsonAndUncachedPodcast_Returns503()
    {
        // Arrange
        await using var factory = new TestWebApplicationFactory(prepopulateCache: false);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts/test-show?format=json");

        // Assert
        response.Should().Be503ServiceUnavailable();
    }

    [Test]
    public async Task GetPodcast_WithFormatJsonAndNonExistentPodcastId_Returns404()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts/non-existent-show?format=json");

        // Assert
        response.Should().Be404NotFound();
    }
}
