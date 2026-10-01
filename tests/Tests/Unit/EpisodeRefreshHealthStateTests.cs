using FluentAssertions;
using NUnit.Framework;
using PodBridge.Logic.Refresh;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class EpisodeRefreshHealthStateTests
{
    private EpisodeRefreshHealthState _testee = null!;

    [SetUp]
    public void Setup()
    {
        _testee = new EpisodeRefreshHealthState();
    }

    [Test]
    public void GetFailingPodcastIds_BeforeAnyResultRecorded_IsEmpty()
    {
        // Act
        var failingPodcastIds = _testee.GetFailingPodcastIds();

        // Assert
        failingPodcastIds.Should().BeEmpty();
    }

    [Test]
    public void RecordResult_Succeeded_DoesNotAppearInFailingPodcastIds()
    {
        // Act
        _testee.RecordResult("show1", succeeded: true);

        // Assert
        _testee.GetFailingPodcastIds().Should().BeEmpty();
    }

    [Test]
    public void RecordResult_Failed_AppearsInFailingPodcastIds()
    {
        // Act
        _testee.RecordResult("show1", succeeded: false);

        // Assert
        _testee.GetFailingPodcastIds().Should().ContainSingle().Which.Should().Be("show1");
    }

    [Test]
    public void RecordResult_FailsThenSucceeds_NoLongerAppearsInFailingPodcastIds()
    {
        // Arrange
        _testee.RecordResult("show1", succeeded: false);

        // Act
        _testee.RecordResult("show1", succeeded: true);

        // Assert
        _testee.GetFailingPodcastIds().Should().BeEmpty();
    }

    [Test]
    public void RecordResult_MultiplePodcastsWithMixedResults_OnlyFailingOnesAppear()
    {
        // Act
        _testee.RecordResult("succeeding-show", succeeded: true);
        _testee.RecordResult("failing-show-1", succeeded: false);
        _testee.RecordResult("failing-show-2", succeeded: false);

        // Assert
        _testee.GetFailingPodcastIds().Should().BeEquivalentTo(["failing-show-1", "failing-show-2"]);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void RecordResult_NullOrWhiteSpacePodcastId_Throws(string? podcastId)
    {
        // Act
        var act = () => _testee.RecordResult(podcastId!, succeeded: true);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
