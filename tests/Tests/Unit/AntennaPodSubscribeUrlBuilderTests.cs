using FluentAssertions;
using NUnit.Framework;
using PodBridge.Logic.Feeds;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public sealed class AntennaPodSubscribeUrlBuilderTests
{
    [Test]
    public void BuildSubscribeUrl_WithHttpsFeedUrl_ReturnsCanonicalDeeplinkWithEscapedUrl()
    {
        // Arrange
        var testee = new AntennaPodSubscribeUrlBuilder();

        // Act
        var subscribeUrl = testee.BuildSubscribeUrl("https://feeds.example.test/api/podcasts/example-show");

        // Assert
        subscribeUrl.Should().Be("https://antennapod.org/deeplink/subscribe/?url=https%3A%2F%2Ffeeds.example.test%2Fapi%2Fpodcasts%2Fexample-show");
    }

    [Test]
    public void BuildSubscribeUrl_WithPortAndPathBase_EscapesPortAndPath()
    {
        // Arrange
        var testee = new AntennaPodSubscribeUrlBuilder();

        // Act
        var subscribeUrl = testee.BuildSubscribeUrl("https://myraspi.local:8080/podbridge/api/podcasts/example-show");

        // Assert
        subscribeUrl.Should().Be("https://antennapod.org/deeplink/subscribe/?url=https%3A%2F%2Fmyraspi.local%3A8080%2Fpodbridge%2Fapi%2Fpodcasts%2Fexample-show");
    }

    [Test]
    public void BuildSubscribeUrl_WithNonAsciiFeedUrl_EscapesUtf8Bytes()
    {
        // Arrange
        var testee = new AntennaPodSubscribeUrlBuilder();

        // Act
        var subscribeUrl = testee.BuildSubscribeUrl("https://feeds.example.test/podcasts/Rückblick");

        // Assert
        subscribeUrl.Should().Be("https://antennapod.org/deeplink/subscribe/?url=https%3A%2F%2Ffeeds.example.test%2Fpodcasts%2FR%C3%BCckblick");
    }

    [Test]
    public void BuildSubscribeUrl_WithNullFeedUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var testee = new AntennaPodSubscribeUrlBuilder();

        // Act
        var act = () => testee.BuildSubscribeUrl(null!);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [TestCase("")]
    [TestCase(" ")]
    public void BuildSubscribeUrl_WithWhitespaceFeedUrl_ThrowsArgumentException(string feedUrl)
    {
        // Arrange
        var testee = new AntennaPodSubscribeUrlBuilder();

        // Act
        var act = () => testee.BuildSubscribeUrl(feedUrl);

        // Assert
        var exception = act.Should().ThrowExactly<ArgumentException>().Which;
        exception.Message.Should().Be("The value cannot be an empty string or composed entirely of whitespace. (Parameter 'feedUrl')");
    }

    [TestCase("/api/podcasts/example-show")]
    [TestCase("feeds.example.test/api/podcasts/example-show")]
    [TestCase("ftp://feeds.example.test/api/podcasts/example-show")]
    [TestCase("javascript:alert(1)")]
    public void BuildSubscribeUrl_WithNonAbsoluteHttpOrHttpsFeedUrl_Throws(string feedUrl)
    {
        // Arrange
        var testee = new AntennaPodSubscribeUrlBuilder();

        // Act
        var act = () => testee.BuildSubscribeUrl(feedUrl);

        // Assert
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().StartWith("Feed URL must be an absolute HTTP or HTTPS URI.");
    }
}
