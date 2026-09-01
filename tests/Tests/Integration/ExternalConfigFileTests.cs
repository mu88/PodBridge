using System.Net;
using FluentAssertions;
using FluentAssertions.Web;
using NUnit.Framework;
using Tests.TestSupport.Builders;

namespace Tests.Integration;

[TestFixture]
[Category("Integration")]
public sealed class ExternalConfigFileTests
{
    private const string EnvironmentVariableName = "PODBRIDGE_EXTERNAL_CONFIG_FILE_PATH";

    private string? _tempFilePath;

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariableName, null);

        if (_tempFilePath is not null && File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    [Test]
    public async Task GetPodcasts_WithExternalConfigFile_AppliesRateLimitOverrideFromFile()
    {
        // Arrange - RateLimitingPermitLimit is deliberately not set via TestWebApplicationFactory's own
        // configuration overrides, so a request being rate-limited proves the value came from the mounted
        // external file rather than from any other configuration source.
        _tempFilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(_tempFilePath, """{ "PodBridge": { "RateLimitingPermitLimit": 1, "RateLimitingWindowMinutes": 1 } }""");
        Environment.SetEnvironmentVariable(EnvironmentVariableName, _tempFilePath);

        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response1 = await client.GetAsync("/api/podcasts");
        using var response2 = await client.GetAsync("/api/podcasts");

        // Assert
        response1.Should().Be200Ok();
        response2.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task GetPodcasts_WithoutExternalConfigFile_UsesDefaultRateLimit()
    {
        // Arrange - no environment variable set, and no file exists at the hardcoded default path, so the
        // optional JSON file provider must silently contribute nothing, leaving the default rate limit in effect.
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response1 = await client.GetAsync("/api/podcasts");
        using var response2 = await client.GetAsync("/api/podcasts");

        // Assert
        response1.Should().Be200Ok();
        response2.Should().Be200Ok();
    }
}
