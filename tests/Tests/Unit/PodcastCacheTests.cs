using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using PodBridge.Logic.Caching;
using Tests.TestSupport.Builders;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class PodcastCacheTests
{
    private FakeTimeProvider _timeProvider = null!;
    private PodcastCache _testee = null!;

    [SetUp]
    public void Setup()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero));
        _testee = new PodcastCache(_timeProvider);
    }

    [Test]
    public void Update_ValidPodcast_StoresPodcast()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();

        // Act
        _testee.Update("fixture-show", podcast);

        // Assert
        var retrieved = _testee.TryGetFull("fixture-show");
        retrieved.Should().NotBeNull();
        retrieved!.Podcast.Title.Should().Be("Fixture Podcast");
    }

    [Test]
    public void Update_ValidPodcast_StampsLastUpdatedWithCurrentTime()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();

        // Act
        _testee.Update("fixture-show", podcast);

        // Assert
        var retrieved = _testee.TryGetFull("fixture-show");
        retrieved!.LastUpdated.Should().Be(_timeProvider.GetUtcNow());
    }

    [Test]
    public void Update_SamePodcastIdTwice_OverwritesPreviousValue()
    {
        // Arrange
        var podcast1 = new PodcastBuilder().WithDefaults().WithTitle("First Title").Build();
        var podcast2 = new PodcastBuilder().WithDefaults().WithTitle("Second Title").Build();

        // Act
        _testee.Update("fixture-show", podcast1);
        _testee.Update("fixture-show", podcast2);

        // Assert
        var retrieved = _testee.TryGetFull("fixture-show");
        retrieved.Should().NotBeNull();
        retrieved!.Podcast.Title.Should().Be("Second Title");
    }

    [Test]
    public void Update_SamePodcastIdTwice_RefreshesLastUpdatedToLatestTime()
    {
        // Arrange
        var podcast1 = new PodcastBuilder().WithDefaults().Build();
        var podcast2 = new PodcastBuilder().WithDefaults().Build();
        _testee.Update("fixture-show", podcast1);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        // Act
        _testee.Update("fixture-show", podcast2);

        // Assert
        var retrieved = _testee.TryGetFull("fixture-show");
        retrieved!.LastUpdated.Should().Be(_timeProvider.GetUtcNow());
    }

    [Test]
    public void TryGetFull_NonExistentPodcastId_ReturnsNull()
    {
        // Act
        var retrieved = _testee.TryGetFull("non-existent-show");

        // Assert
        retrieved.Should().BeNull();
    }

    [Test]
    public void Update_NullPodcastId_ThrowsArgumentException()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();

        // Act
        var act = () => _testee.Update(null!, podcast);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Update_WhitespacePodcastId_ThrowsArgumentException()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();

        // Act
        var act = () => _testee.Update("   ", podcast);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Update_NullPodcast_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _testee.Update("fixture-show", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public async Task Cache_IsThreadSafe_MultipleThreadsCanUpdateConcurrently()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() => _testee.Update($"show-{i}", podcast)));

        // Act
        var act = async () => await Task.WhenAll(tasks);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
