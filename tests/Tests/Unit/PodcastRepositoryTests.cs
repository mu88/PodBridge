using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using NUnit.Framework;
using PodBridge.Logic.Config;
using PodBridge.Logic.Shared;
using PodBridge.Persistence;
using Tests.TestSupport;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
[SuppressMessage("IDisposableAnalyzers.Correctness", "IDISP006:Implement IDisposable", Justification = "NUnit tear-down disposes the per-test SQLite scope.")]
public sealed class PodcastRepositoryTests
{
    private SqliteAppDbContextFactoryScope? _dbContextFactoryScope;
    private PodcastRepository _testee = null!;

    [TearDown]
    public void TearDown()
    {
        _dbContextFactoryScope?.Dispose();
    }

    [Test]
    public async Task ExistsWithPodcastIdAsync_WithExistingPodcastId_ReturnsTrue()
    {
        // Arrange
        ConfigureServices(new PodcastConfig("existing-podcast", "show-1"));

        // Act
        var result = await _testee.ExistsWithPodcastIdAsync("existing-podcast", CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task ExistsWithPodcastIdAsync_WithUnknownPodcastId_ReturnsFalse()
    {
        // Arrange
        ConfigureServices();

        // Act
        var result = await _testee.ExistsWithPodcastIdAsync("unknown-podcast", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public async Task AddAsync_WithNewPodcast_PersistsItAndReturnsAdded()
    {
        // Arrange
        ConfigureServices();
        var podcast = new PodcastConfig("new-podcast", "show-1");

        // Act
        var outcome = await _testee.AddAsync(podcast, CancellationToken.None);

        // Assert
        outcome.Should().Be(PodcastRepositoryAddOutcome.Added);
        var stored = await _testee.GetAllAsync(CancellationToken.None);
        stored.Should().ContainSingle(configuredPodcast => configuredPodcast.PodcastId == "new-podcast" && configuredPodcast.ShowId == "show-1");
    }

    [Test]
    public async Task AddAsync_WithAlreadyExistingShowId_ReturnsShowIdAlreadyExistsWithoutDuplicating()
    {
        // Arrange - seeds an existing podcast with the same ShowId so the unique index violation is
        // triggered organically, exercising the real DbUpdateException path instead of a mock.
        ConfigureServices(new PodcastConfig("existing-podcast", "duplicate-show"));

        // Act
        var outcome = await _testee.AddAsync(new PodcastConfig("another-podcast", "duplicate-show"), CancellationToken.None);

        // Assert
        outcome.Should().Be(PodcastRepositoryAddOutcome.ShowIdAlreadyExists);
        var stored = await _testee.GetAllAsync(CancellationToken.None);
        stored.Should().ContainSingle();
    }

    [Test]
    public async Task AddAsync_WithAlreadyExistingPodcastId_ReturnsFailed()
    {
        // Arrange - a PodcastId collision (distinct from a ShowId collision above) has no "ShowId" marker
        // in the resulting exception, exercising the generic Failed fallback.
        ConfigureServices(new PodcastConfig("colliding-podcast", "show-1"));

        // Act
        var outcome = await _testee.AddAsync(new PodcastConfig("colliding-podcast", "show-2"), CancellationToken.None);

        // Assert
        outcome.Should().Be(PodcastRepositoryAddOutcome.Failed);
    }

    private void ConfigureServices(params PodcastConfig[] existingPodcasts)
    {
        _dbContextFactoryScope?.Dispose();
        _dbContextFactoryScope = new SqliteAppDbContextFactoryScope(existingPodcasts);
        _testee = new PodcastRepository(_dbContextFactoryScope.DbContextFactory);
    }
}
