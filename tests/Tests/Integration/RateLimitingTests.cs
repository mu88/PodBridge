using System.Net;
using FluentAssertions;
using NUnit.Framework;
using Tests.TestSupport.Builders;

namespace Tests.Integration;

[TestFixture]
[Category("Integration")]
public sealed class RateLimitingTests
{
    [Test]
    public async Task GetPodcast_ExceedingRateLimit_Returns503()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            rateLimitingPermitLimit: 2,
            rateLimitingWindowMinutes: 1);
        using var client = factory.CreateClient();

        // Act - make requests exceeding the limit
        using var response1 = await client.GetAsync("/api/podcasts/test-show");
        using var response2 = await client.GetAsync("/api/podcasts/test-show");
        using var response3 = await client.GetAsync("/api/podcasts/test-show");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        response3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task GetPodcasts_ExceedingRateLimit_Returns503()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(
            testPodcast: podcast,
            rateLimitingPermitLimit: 2,
            rateLimitingWindowMinutes: 1);
        using var client = factory.CreateClient();

        // Act
        using var response1 = await client.GetAsync("/api/podcasts");
        using var response2 = await client.GetAsync("/api/podcasts");
        using var response3 = await client.GetAsync("/api/podcasts");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        response3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
