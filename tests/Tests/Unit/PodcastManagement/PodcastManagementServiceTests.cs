using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using PodBridge.Logic.Config;
using PodBridge.Logic.EpisodeSourcing;
using PodBridge.Logic.PodcastManagement;
using PodBridge.Logic.Refresh;
using PodBridge.Logic.Shared;

namespace Tests.Unit.PodcastManagement;

[TestFixture]
[Category("Unit")]
public sealed class PodcastManagementServiceTests
{
    private IPodcastDirectory _podcastDirectory = null!;
    private IPodcastRepository _podcastRepository = null!;
    private IPodcastRefreshService _podcastRefreshService = null!;
    private PodcastManagementService _testee = null!;

    [SetUp]
    public void SetUp()
    {
        _podcastDirectory = Substitute.For<IPodcastDirectory>();
        _podcastRepository = Substitute.For<IPodcastRepository>();
        _podcastRefreshService = Substitute.For<IPodcastRefreshService>();
        _podcastRepository.ExistsWithPodcastIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _podcastRepository.AddAsync(Arg.Any<PodcastConfig>(), Arg.Any<CancellationToken>()).Returns(PodcastRepositoryAddOutcome.Added);
        _podcastRefreshService.RefreshOneAsync(Arg.Any<PodcastConfig>(), Arg.Any<CancellationToken>()).Returns(true);

        _testee = new PodcastManagementService(
            _podcastDirectory,
            _podcastRepository,
            _podcastRefreshService,
            NullLogger<PodcastManagementService>.Instance);
    }

    [Test]
    public async Task AddAsync_WithKnownTitle_InsertsPodcastAndTriggersBackfill()
    {
        // Act
        var result = await _testee.AddAsync("show-1", "Some Show", CancellationToken.None);

        // Assert
        result.Should().Be(new PodcastAddResult(PodcastAddStatus.Added, "Some Show", "some-show"));
        await _podcastRepository.Received(1).AddAsync(
            Arg.Is<PodcastConfig>(podcast => podcast.ShowId == "show-1" && podcast.PodcastId == "some-show"),
            Arg.Any<CancellationToken>());
        await _podcastRefreshService.Received(1).RefreshOneAsync(
            Arg.Is<PodcastConfig>(podcast => podcast.ShowId == "show-1" && podcast.PodcastId == "some-show"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddByShowIdAsync_WithKnownShow_ResolvesPreviewTitleAndAdds()
    {
        // Arrange
        _podcastDirectory.FindPreviewAsync("manual-show", Arg.Any<CancellationToken>())
            .Returns(new PodcastPreview("Manual Show", null));

        // Act
        var result = await _testee.AddByShowIdAsync("manual-show", CancellationToken.None);

        // Assert
        result.Should().Be(new PodcastAddResult(PodcastAddStatus.Added, "Manual Show", "manual-show"));
    }

    [Test]
    public async Task AddByShowIdAsync_WithUnknownShow_ReturnsShowIdNotFound()
    {
        // Arrange
        _podcastDirectory.FindPreviewAsync("unknown-show", Arg.Any<CancellationToken>())
            .Returns((PodcastPreview?)null);

        // Act
        var result = await _testee.AddByShowIdAsync("unknown-show", CancellationToken.None);

        // Assert
        result.Should().Be(new PodcastAddResult(PodcastAddStatus.ShowIdNotFound));
        await _podcastRepository.DidNotReceive().AddAsync(Arg.Any<PodcastConfig>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddByShowIdAsync_WhenDirectoryThrows_ReturnsFailedWithoutAdding()
    {
        // Arrange
        _podcastDirectory.FindPreviewAsync("broken-show", Arg.Any<CancellationToken>())
            .Returns<PodcastPreview?>(_ => throw new HttpRequestException("boom"));

        // Act
        var result = await _testee.AddByShowIdAsync("broken-show", CancellationToken.None);

        // Assert
        result.Should().Be(new PodcastAddResult(PodcastAddStatus.Failed));
        await _podcastRepository.DidNotReceive().AddAsync(Arg.Any<PodcastConfig>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddAsync_WithAlreadyAddedShowId_ReturnsShowIdAlreadyExistsWithoutTriggeringBackfill()
    {
        // Arrange
        _podcastRepository.AddAsync(Arg.Any<PodcastConfig>(), Arg.Any<CancellationToken>())
            .Returns(PodcastRepositoryAddOutcome.ShowIdAlreadyExists);

        // Act
        var result = await _testee.AddAsync("duplicate-show", "Duplicate Show", CancellationToken.None);

        // Assert
        result.Should().Be(new PodcastAddResult(PodcastAddStatus.ShowIdAlreadyExists, "Duplicate Show"));
        await _podcastRefreshService.DidNotReceive().RefreshOneAsync(Arg.Any<PodcastConfig>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddAsync_WhenRepositoryAddFails_ReturnsFailedWithoutTriggeringBackfill()
    {
        // Arrange
        _podcastRepository.AddAsync(Arg.Any<PodcastConfig>(), Arg.Any<CancellationToken>())
            .Returns(PodcastRepositoryAddOutcome.Failed);

        // Act
        var result = await _testee.AddAsync("show-1", "Some Show", CancellationToken.None);

        // Assert
        result.Should().Be(new PodcastAddResult(PodcastAddStatus.Failed, "Some Show"));
        await _podcastRefreshService.DidNotReceive().RefreshOneAsync(Arg.Any<PodcastConfig>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddAsync_WithTitleCollidingWithExistingPodcastId_AppendsNumericSuffix()
    {
        // Arrange
        _podcastRepository.ExistsWithPodcastIdAsync("manual-show", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _testee.AddAsync("manual-show-2", "Manual Show", CancellationToken.None);

        // Assert
        result.PodcastId.Should().Be("manual-show-2");
    }

    [Test]
    public async Task AddAsync_WithTitleContainingMultipleConsecutiveSpecialCharacters_CollapsesRunsToASingleHyphen()
    {
        // Arrange: three consecutive spaces produce "---" after non-alphanumeric replacement, which needs
        // more than one pass of the "--" -> "-" collapsing loop in Slugify to fully reduce to a single "-".

        // Act
        var result = await _testee.AddAsync("multi-gap-show", "Show   Title", CancellationToken.None);

        // Assert
        result.PodcastId.Should().Be("show-title");
    }

    [Test]
    public async Task AddAsync_WithTitleContainingOnlySpecialCharacters_FallsBackToGeneratedGuidAsPodcastId()
    {
        // Arrange: a title with no alphanumeric characters at all collapses to an empty string after
        // Slugify's hyphen-collapsing and trimming, exercising the "IsNullOrWhiteSpace(slug)" GUID fallback.

        // Act
        var result = await _testee.AddAsync("special-chars-show", "!!!???", CancellationToken.None);

        // Assert
        Guid.TryParse(result.PodcastId, out _).Should().BeTrue();
    }
}
