using FluentAssertions;
using FluentAssertions.Web;
using NUnit.Framework;
using Tests.TestSupport.Builders;

namespace Tests.Integration;

[TestFixture]
[Category("Integration")]
public sealed class PathBaseTests
{
    [Test]
    public async Task GetRoot_WithPathBase_RedirectsCorrectly()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            pathBase: "/podbridge");
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/podbridge");

        // Assert
        response.Should().Be200Ok();
    }

    [Test]
    public async Task GetFeed_WithPathBase_ReturnsCorrectFeed()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            pathBase: "/podbridge");
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/podbridge/api/podcasts/test-show");

        // Assert
        response.Should().Be200Ok();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("<rss");
    }
}
