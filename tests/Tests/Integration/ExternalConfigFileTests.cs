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
    public async Task GetRoot_WithExternalConfigFile_AppliesPathBaseFromFile()
    {
        // Arrange - PathBase is deliberately not set via TestWebApplicationFactory's own configuration
        // overrides, so a successful response at that path base proves the value came from the mounted
        // external file rather than from any other configuration source.
        _tempFilePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(_tempFilePath, """{ "PodBridge": { "PathBase": "/from-external-file" } }""");
        Environment.SetEnvironmentVariable(EnvironmentVariableName, _tempFilePath);

        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/from-external-file");

        // Assert
        response.Should().Be200Ok();
    }

    [Test]
    public async Task GetRoot_WithoutExternalConfigFile_DoesNotApplyAnyPathBase()
    {
        // Arrange - no environment variable set, and no file exists at the hardcoded default path, so the
        // optional JSON file provider must silently contribute nothing.
        var podcast = new PodcastBuilder().WithDefaults().Build();
        await using var factory = new TestWebApplicationFactory(testPodcast: podcast);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/");

        // Assert
        response.Should().Be200Ok();
    }
}
