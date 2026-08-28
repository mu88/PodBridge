using FluentAssertions;
using NUnit.Framework;
using Tests.TestSupport.Builders;

namespace Tests.Integration;

[TestFixture]
[Category("Integration")]
public sealed class SecurityHeadersTests
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        var podcast = new PodcastBuilder().WithDefaults().Build();
        _factory = new TestWebApplicationFactory(testPodcast: podcast);
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task HealthEndpoint_ReturnsSecurityHeaders()
    {
        // Act
        using var response = await _client.GetAsync("/healthz");

        // Assert
        response.Headers.Should().ContainKey("X-Frame-Options")
            .WhoseValue.Should().ContainSingle().Which.Should().Be("DENY");
        response.Headers.Should().ContainKey("X-Content-Type-Options")
            .WhoseValue.Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.Should().ContainKey("Referrer-Policy")
            .WhoseValue.Should().ContainSingle().Which.Should().Be("no-referrer");
        response.Headers.Should().ContainKey("Content-Security-Policy")
            .WhoseValue.Should().ContainSingle().Which.Should().Be("default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' https:");
    }
}

