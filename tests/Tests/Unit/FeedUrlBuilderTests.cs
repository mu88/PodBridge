using FluentAssertions;
using NUnit.Framework;
using PodBridge.Logic.Feeds;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public sealed class FeedUrlBuilderTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void BuildFeedUrl_WithNullOrWhitespacePodcastId_Throws(string? podcastId)
    {
        // Arrange
        var testee = new FeedUrlBuilder();

        // Act
        var act = () => testee.BuildFeedUrl(podcastId!, "https://myraspi.local/podbridge");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void BuildFeedUrl_WithRequestBaseUrl_UsesRequestBaseUrl()
    {
        // Arrange
        var testee = new FeedUrlBuilder();

        // Act
        var feedUrl = testee.BuildFeedUrl("example-show", "https://myraspi.local/podbridge");

        // Assert
        feedUrl.Should().Be("https://myraspi.local/podbridge/api/podcasts/example-show");
    }

    [Test]
    public void BuildFeedUrl_WithTrailingSlashOnRequestBaseUrl_TrimsSlashBeforeAppendingPath()
    {
        // Arrange
        var testee = new FeedUrlBuilder();

        // Act
        var feedUrl = testee.BuildFeedUrl("example-show", "https://myraspi.local/podbridge/");

        // Assert
        feedUrl.Should().Be("https://myraspi.local/podbridge/api/podcasts/example-show");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void BuildFeedUrl_WithNullOrWhitespaceRequestBaseUrl_Throws(string? requestBaseUrl)
    {
        // Arrange
        var testee = new FeedUrlBuilder();

        // Act
        var act = () => testee.BuildFeedUrl("example-show", requestBaseUrl!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}

