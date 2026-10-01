using System.Net.Http.Json;
using FluentAssertions;
using FluentAssertions.Web;
using NSubstitute;
using NUnit.Framework;
using PodBridge.Logic.EpisodeSourcing;

namespace Tests.Integration;

[TestFixture]
[Category("Integration")]
public sealed class PodcastManagementEndpointsTests
{
    [Test]
    public async Task SearchPodcasts_WithMatchingShows_ReturnsResults()
    {
        // Arrange
        var podcastDirectory = Substitute.For<IPodcastDirectory>();
        podcastDirectory.SearchAsync("some show", Arg.Any<CancellationToken>())
            .Returns([new PodcastSearchResult("show-1", "Some Show", 12)]);
        await using var factory = CreateFactory(podcastDirectory);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts/search?query=some%20show");

        // Assert
        response.Should().Be200Ok();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Some Show").And.Contain("show-1");
    }

    [Test]
    public async Task SearchPodcasts_WhenDirectoryThrows_ReturnsBadGateway()
    {
        // Arrange
        var podcastDirectory = Substitute.For<IPodcastDirectory>();
        podcastDirectory.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<PodcastSearchResult>>(_ => throw new HttpRequestException("boom"));
        await using var factory = CreateFactory(podcastDirectory);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/podcasts/search?query=some%20show");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadGateway);
    }

    [Test]
    public async Task AddPodcast_WithKnownTitle_ReturnsCreatedAndPersists()
    {
        // Arrange
        await using var factory = CreateFactory(Substitute.For<IPodcastDirectory>());
        using var client = factory.CreateClient();

        // Act
        using var response = await client.PostAsJsonAsync("/api/podcasts", new { ShowId = "new-show", Title = "New Show" });

        // Assert
        response.Should().Be201Created();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("new-show").And.Contain("\"Added\"");

        using var getResponse = await client.GetAsync("/api/podcasts");
        var getContent = await getResponse.Content.ReadAsStringAsync();
        getContent.Should().Contain("new-show");
    }

    [Test]
    public async Task AddPodcast_ByShowIdWithUnknownShow_ReturnsNotFound()
    {
        // Arrange
        var podcastDirectory = Substitute.For<IPodcastDirectory>();
        podcastDirectory.FindPreviewAsync("unknown-show", Arg.Any<CancellationToken>()).Returns((PodcastPreview?)null);
        await using var factory = CreateFactory(podcastDirectory);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.PostAsJsonAsync("/api/podcasts", new { ShowId = "unknown-show" });

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Test]
    public async Task AddPodcast_WithAlreadyExistingShowId_ReturnsConflict()
    {
        // Arrange
        var podcastConfigs = new List<PodBridge.Logic.Config.PodcastConfig>
        {
            new Tests.TestSupport.Builders.PodcastConfigBuilder().WithDefaults().WithShowId("duplicate-show").Build(),
        };
        await using var factory = CreateFactory(Substitute.For<IPodcastDirectory>(), podcastConfigs);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.PostAsJsonAsync("/api/podcasts", new { ShowId = "duplicate-show", Title = "Duplicate Show" });

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }

    [Test]
    public async Task AddPodcast_WhenServiceReportsFailure_ReturnsBadGateway()
    {
        // Arrange
        var podcastManagementService = Substitute.For<PodBridge.Logic.PodcastManagement.IPodcastManagementService>();
        podcastManagementService.AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PodBridge.Logic.PodcastManagement.PodcastAddResult(PodBridge.Logic.PodcastManagement.PodcastAddStatus.Failed));
        await using var factory = CreateFactory(Substitute.For<IPodcastDirectory>(), podcastManagementService: podcastManagementService);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.PostAsJsonAsync("/api/podcasts", new { ShowId = "some-show", Title = "Some Show" });

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadGateway);
    }

    private static TestWebApplicationFactory CreateFactory(
        IPodcastDirectory podcastDirectory,
        IReadOnlyList<PodBridge.Logic.Config.PodcastConfig>? podcasts = null,
        PodBridge.Logic.PodcastManagement.IPodcastManagementService? podcastManagementService = null) =>
        new(podcasts: podcasts ?? [], podcastDirectory: podcastDirectory, podcastManagementService: podcastManagementService);
}
