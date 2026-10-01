using FluentAssertions;
using NUnit.Framework;

namespace Tests.Integration;

[TestFixture]
[Category("Integration")]
public sealed class HealthCheckEndpointTests
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task HealthEndpoint_WithoutAnyRecordedRefreshFailure_ReturnsHealthy()
    {
        // Act
        using var response = await _client.GetAsync("/healthz");
        var body = await response.Content.ReadAsStringAsync();

        // Assert: the episode-refresh health check (see EpisodeRefreshHealthCheck) is wired up via
        // Program.cs and must not report Degraded/Unhealthy before any refresh ever ran.
        response.IsSuccessStatusCode.Should().BeTrue();
        body.Should().Be("Healthy");
    }
}
