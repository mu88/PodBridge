using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NUnit.Framework;
using PodBridge.Api.Refresh;
using PodBridge.Logic.Refresh;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class EpisodeRefreshHealthCheckTests
{
    private IEpisodeRefreshHealthState _healthStateMock = null!;
    private EpisodeRefreshHealthCheck _testee = null!;

    [SetUp]
    public void Setup()
    {
        _healthStateMock = Substitute.For<IEpisodeRefreshHealthState>();
        _testee = new EpisodeRefreshHealthCheck(_healthStateMock);
    }

    [Test]
    public async Task CheckHealthAsync_NoFailingPodcasts_ReturnsHealthy()
    {
        // Arrange
        _healthStateMock.GetFailingPodcastIds().Returns(Array.Empty<string>());

        // Act
        var result = await _testee.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Test]
    public async Task CheckHealthAsync_AtLeastOneFailingPodcast_ReturnsDegradedWithPodcastIdsInDescription()
    {
        // Arrange
        _healthStateMock.GetFailingPodcastIds().Returns(["failing-show-1", "failing-show-2"]);

        // Act
        var result = await _testee.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("Episode refresh failed for: failing-show-1, failing-show-2");
    }
}
