using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;
using PodBridge.Api.Rss;
using Tests.TestSupport.Builders;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class RssFeedTests
{
    [Test]
    public void MapFrom_ValidPodcast_CreatesRssFeed()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder()
            .WithDefaults()
            .WithTitle("Fixture Podcast")
            .WithEpisodes(episode)
            .Build();

        // Act
        var feed = RssFeed.MapFrom(podcast);

        // Assert
        feed.Should().NotBeNull();
        feed.Channel.Title.Should().Be("Fixture Podcast");
        feed.Channel.Items.Should().HaveCount(1);
    }

    [Test]
    public void MapFrom_PodcastWithMultipleEpisodes_OrdersByPublishDateDescending()
    {
        // Arrange
        var episode1 = new EpisodeBuilder().WithDefaults().Build() with
        {
            Title = "Older Episode",
            PublishDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var episode2 = new EpisodeBuilder().WithDefaults().Build() with
        {
            Title = "Newer Episode",
            PublishDate = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var podcast = new PodcastBuilder()
            .WithDefaults()
            .WithEpisodes(episode1, episode2)
            .Build();

        // Act
        var feed = RssFeed.MapFrom(podcast);

        // Assert
        feed.Channel.Items.Should().HaveCount(2);
        feed.Channel.Items[0].Title.Should().Be("Newer Episode");
        feed.Channel.Items[1].Title.Should().Be("Older Episode");
    }

    [Test]
    public void MapFrom_PodcastWithNullDescription_FallsBackToTitle()
    {
        // Arrange
        var podcast = new PodcastBuilder()
            .WithDefaults()
            .WithTitle("Fixture Podcast")
            .Build() with { Description = null };

        // Act
        var feed = RssFeed.MapFrom(podcast);

        // Assert
        feed.Channel.Description.Should().Be("Fixture Podcast");
    }

    [Test]
    public void MapFrom_PodcastWithNullLink_UsesEmptyString()
    {
        // Arrange
        var podcast = new PodcastBuilder()
            .WithDefaults()
            .Build() with { Link = null };

        // Act
        var feed = RssFeed.MapFrom(podcast);

        // Assert
        feed.Channel.Link.Should().BeEmpty();
        feed.Channel.Image!.Link.Should().BeEmpty();
    }

    [Test]
    public void MapFrom_PodcastWithNullImageUrl_OmitsImage()
    {
        // Arrange
        var podcast = new PodcastBuilder()
            .WithDefaults()
            .Build() with { ImageUrl = null };

        // Act
        var feed = RssFeed.MapFrom(podcast);

        // Assert
        feed.Channel.Image.Should().BeNull();
        feed.Channel.ItunesImage.Should().BeNull();
    }

    [Test]
    public void MapFrom_PodcastWithImageUrl_SetsChannelImageAndItunesImage()
    {
        // Arrange
        var podcast = new PodcastBuilder()
            .WithDefaults()
            .WithTitle("Fixture Podcast")
            .Build() with { ImageUrl = new Uri("https://fixture.test/podcast.jpg") };

        // Act
        var feed = RssFeed.MapFrom(podcast);

        // Assert
        feed.Channel.Image.Should().NotBeNull();
        feed.Channel.Image!.Url.Should().Be("https://fixture.test/podcast.jpg");
        feed.Channel.Image.Title.Should().Be("Fixture Podcast");
        feed.Channel.ItunesImage.Should().NotBeNull();
        feed.Channel.ItunesImage!.Href.Should().Be("https://fixture.test/podcast.jpg");
    }

    [Test]
    public void MapFrom_PodcastWithNullAuthor_FallsBackToTitle()
    {
        // Arrange
        var podcast = new PodcastBuilder()
            .WithDefaults()
            .WithTitle("Fixture Podcast")
            .Build() with { Author = null };

        // Act
        var feed = RssFeed.MapFrom(podcast);

        // Assert
        feed.Channel.ItunesAuthor.Should().Be("Fixture Podcast");
    }

    [Test]
    public void MapFrom_EpisodeWithAllMetadata_MapsAllFields()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build() with { Title = "Full Metadata Episode", Guid = "full-guid" };
        var podcast = new PodcastBuilder()
            .WithDefaults()
            .WithEpisodes(episode)
            .Build();

        // Act
        var feed = RssFeed.MapFrom(podcast);

        // Assert
        var item = feed.Channel.Items[0];
        item.Title.Should().Be("Full Metadata Episode");
        item.Guid.Should().Be("full-guid");
        item.Enclosure.Should().NotBeNull();
        item.ItunesImage.Should().NotBeNull();
        item.ItunesDuration.Should().NotBeNull();
        item.ItunesEpisode.Should().NotBeNull();
    }

    [Test]
    public void MapFrom_EpisodeWithMinimalMetadata_MapsRequiredFieldsOnly()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().WithMinimalMetadata().Build();
        var podcast = new PodcastBuilder()
            .WithDefaults()
            .WithEpisodes(episode)
            .Build();

        // Act
        var feed = RssFeed.MapFrom(podcast);

        // Assert
        var item = feed.Channel.Items[0];
        item.Title.Should().Be("Fixture Episode");
        item.Description.Should().BeNull();
        item.ItunesImage.Should().BeNull();
        item.ItunesDuration.Should().BeNull();
        item.ItunesEpisode.Should().BeNull();
    }

    [Test]
    public void Serialize_ValidFeed_ProducesValidXml()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder()
            .WithDefaults()
            .WithEpisodes(episode)
            .Build();
        var feed = RssFeed.MapFrom(podcast);

        // Act
        var xml = RssFeedSerializer.Serialize(feed);

        // Assert
        var doc = XDocument.Parse(xml);
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("rss");
        doc.Root.Attribute("version")!.Value.Should().Be("2.0");
    }

    [Test]
    public void Serialize_NullFeed_ThrowsArgumentNullException()
    {
        // Act
        var act = () => RssFeedSerializer.Serialize(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Serialize_Output_UsesTwoSpaceIndentationAndUnixLineEndings()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        var feed = RssFeed.MapFrom(podcast);

        // Act
        var xml = RssFeedSerializer.Serialize(feed);

        // Assert - only present with Indent=true, IndentChars="  " and NewLineChars="\n" all in effect together.
        xml.Should().NotContain("\r\n");
        xml.Should().Contain("\n  <channel>");
        xml.Should().Contain("\n    <title>");
    }

    [Test]
    public void Serialize_Output_EndsWithSingleTrailingNewline()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        var feed = RssFeed.MapFrom(podcast);

        // Act
        var xml = RssFeedSerializer.Serialize(feed);

        // Assert
        xml.Should().EndWith("</rss>\n");
        xml.Should().NotEndWith("</rss>\n\n");
    }

    [Test]
    public void Serialize_Output_UsesItunesNamespacePrefix()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        var feed = RssFeed.MapFrom(podcast);

        // Act
        var xml = RssFeedSerializer.Serialize(feed);

        // Assert
        xml.Should().Contain("<itunes:author>");
        xml.Should().Contain("xmlns:itunes=\"" + RssXmlNamespaces.Itunes + "\"");
    }

    [Test]
    public void Serialize_WithSelfLinkUrl_InjectsAtomSelfLink()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        var feed = RssFeed.MapFrom(podcast);

        // Act
        var xml = RssFeedSerializer.Serialize(feed, "https://fixture.test/feeds/test");

        // Assert
        var doc = XDocument.Parse(xml);
        var atomNamespace = XNamespace.Get(RssXmlNamespaces.Atom);
        var atomLink = doc.Descendants(atomNamespace + "link").FirstOrDefault();
        atomLink.Should().NotBeNull();
        atomLink!.Attribute("href")!.Value.Should().Be("https://fixture.test/feeds/test");
        atomLink.Attribute("rel")!.Value.Should().Be("self");
        atomLink.Attribute("type")!.Value.Should().Be("application/rss+xml");
    }

    [Test]
    public void Serialize_WithoutSelfLinkUrl_OmitsAtomSelfLink()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        var feed = RssFeed.MapFrom(podcast);

        // Act
        var xml = RssFeedSerializer.Serialize(feed, null);

        // Assert
        var doc = XDocument.Parse(xml);
        var atomNamespace = XNamespace.Get(RssXmlNamespaces.Atom);
        var atomLink = doc.Descendants(atomNamespace + "link").FirstOrDefault();
        atomLink.Should().BeNull();
    }

    [Test]
    public void Serialize_DefaultConstructedDtos_EmitsDeclaredDefaultValues()
    {
        // Arrange - every RSS DTO is constructed via its parameterless constructor, bypassing RssFeed.MapFrom
        // (which always overwrites every field), so the compile-time default values below are actually observed.
        var feed = new RssFeed
        {
            Channel = new RssChannel
            {
                Image = new RssImage(),
                ItunesImage = new RssItunesImage(),
                AtomSelfLink = new AtomLink(),
                Items = [new RssItem { Enclosure = new RssEnclosure(), ItunesImage = new RssItunesImage() }],
            },
        };

        // Act
        var xml = RssFeedSerializer.Serialize(feed);
        var doc = XDocument.Parse(xml);
        var itunesNamespace = XNamespace.Get(RssXmlNamespaces.Itunes);
        var atomNamespace = XNamespace.Get(RssXmlNamespaces.Atom);

        // Assert
        var channel = doc.Root!.Element("channel")!;
        channel.Element("title")!.Value.Should().BeEmpty();
        channel.Element("link")!.Value.Should().BeEmpty();
        channel.Element("description")!.Value.Should().BeEmpty();
        channel.Element(itunesNamespace + "author")!.Value.Should().BeEmpty();
        channel.Element(itunesNamespace + "type")!.Value.Should().Be("episodic");
        channel.Element(itunesNamespace + "explicit")!.Value.Should().Be("no");

        var image = channel.Element("image")!;
        image.Element("url")!.Value.Should().BeEmpty();
        image.Element("title")!.Value.Should().BeEmpty();
        image.Element("link")!.Value.Should().BeEmpty();

        channel.Element(itunesNamespace + "image")!.Attribute("href")!.Value.Should().BeEmpty();
        channel.Element(atomNamespace + "link")!.Attribute("href")!.Value.Should().BeEmpty();

        var item = channel.Element("item")!;
        item.Element("title")!.Value.Should().BeEmpty();
        item.Element("guid")!.Value.Should().BeEmpty();
        item.Element("pubDate")!.Value.Should().BeEmpty();

        var enclosure = item.Element("enclosure")!;
        enclosure.Attribute("url")!.Value.Should().BeEmpty();
        enclosure.Attribute("type")!.Value.Should().Be("audio/mpeg");
    }

    [Test]
    public void Serialize_IncludesItunesNamespace()
    {
        // Arrange
        var podcast = new PodcastBuilder().WithDefaults().Build();
        var feed = RssFeed.MapFrom(podcast);

        // Act
        var xml = RssFeedSerializer.Serialize(feed);

        // Assert
        var doc = XDocument.Parse(xml);
        var itunesNamespace = XNamespace.Get(RssXmlNamespaces.Itunes);
        var itunesAuthor = doc.Descendants(itunesNamespace + "author").FirstOrDefault();
        itunesAuthor.Should().NotBeNull();
    }

    [Test]
    public void RssEnclosure_HasNoLengthAttribute()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        var feed = RssFeed.MapFrom(podcast);

        // Act
        var xml = RssFeedSerializer.Serialize(feed);

        // Assert
        var doc = XDocument.Parse(xml);
        var enclosure = doc.Descendants("enclosure").FirstOrDefault();
        enclosure.Should().NotBeNull();
        enclosure!.Attribute("length").Should().BeNull("EnclosureLengthBytes property was removed from Episode");
    }

    [Test]
    public void RssEnclosure_UsesEpisodeAudioMimeType()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build() with { AudioMimeType = "audio/ogg" };
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        var feed = RssFeed.MapFrom(podcast);

        // Act
        var xml = RssFeedSerializer.Serialize(feed);

        // Assert
        var doc = XDocument.Parse(xml);
        var enclosure = doc.Descendants("enclosure").FirstOrDefault();
        enclosure.Should().NotBeNull();
        enclosure!.Attribute("type")!.Value.Should().Be("audio/ogg");
    }

    [Test]
    public void MapFrom_NullPodcast_ThrowsArgumentNullException()
    {
        // Act
        var act = () => RssFeed.MapFrom(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void Serialize_FullyPopulatedFeed_MapsEveryElementAndAttributeToItsExpectedXmlName()
    {
        // Arrange - builder defaults already give every field a value distinct from its siblings, so a
        // mutated element/attribute name or a swapped/blanked value shows up as a wrong or missing node below.
        var episode = new EpisodeBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        var feed = RssFeed.MapFrom(podcast);

        // Act
        var xml = RssFeedSerializer.Serialize(feed, "https://fixture.test/feeds/full");
        var doc = XDocument.Parse(xml);
        var itunesNamespace = XNamespace.Get(RssXmlNamespaces.Itunes);
        var atomNamespace = XNamespace.Get(RssXmlNamespaces.Atom);

        // Assert
        var channel = doc.Root!.Element("channel")!;
        channel.Element("title")!.Value.Should().Be(podcast.Title);
        channel.Element("link")!.Value.Should().Be(podcast.Link!.OriginalString);
        channel.Element("description")!.Value.Should().Be(podcast.Description);
        channel.Element("language")!.Value.Should().Be(podcast.Language);

        var channelImage = channel.Element("image")!;
        channelImage.Element("url")!.Value.Should().Be(podcast.ImageUrl!.OriginalString);
        channelImage.Element("title")!.Value.Should().Be(podcast.Title);
        channelImage.Element("link")!.Value.Should().Be(podcast.Link.OriginalString);

        channel.Element(itunesNamespace + "image")!.Attribute("href")!.Value.Should().Be(podcast.ImageUrl.OriginalString);
        channel.Element(itunesNamespace + "author")!.Value.Should().Be(podcast.Author);
        channel.Element(itunesNamespace + "type")!.Value.Should().Be("episodic");
        channel.Element(itunesNamespace + "explicit")!.Value.Should().Be("no");

        var atomLink = channel.Element(atomNamespace + "link")!;
        atomLink.Attribute("rel")!.Value.Should().Be("self");
        atomLink.Attribute("href")!.Value.Should().Be("https://fixture.test/feeds/full");
        atomLink.Attribute("type")!.Value.Should().Be("application/rss+xml");

        var item = channel.Element("item")!;
        item.Element("title")!.Value.Should().Be(episode.Title);
        item.Element("guid")!.Value.Should().Be(episode.Guid);
        item.Element("pubDate")!.Value.Should().EndWith("GMT");
        item.Element("description")!.Value.Should().Be(episode.Description);
        item.Element("link")!.Value.Should().Be(episode.Link!.OriginalString);

        var enclosure = item.Element("enclosure")!;
        enclosure.Attribute("url")!.Value.Should().Be(episode.AudioUrl.OriginalString);
        enclosure.Attribute("type")!.Value.Should().Be(episode.AudioMimeType);

        item.Element(itunesNamespace + "image")!.Attribute("href")!.Value.Should().Be(episode.ImageUrl!.OriginalString);
        item.Element(itunesNamespace + "duration")!.Value.Should().Be("1800");
        item.Element(itunesNamespace + "episode")!.Value.Should().Be(episode.EpisodeNumber);
    }

    [Test]
    public void RssItem_HasNoItunesSeasonElement()
    {
        // Arrange
        var episode = new EpisodeBuilder().WithDefaults().Build();
        var podcast = new PodcastBuilder().WithDefaults().WithEpisodes(episode).Build();
        var feed = RssFeed.MapFrom(podcast);

        // Act
        var xml = RssFeedSerializer.Serialize(feed);

        // Assert
        var doc = XDocument.Parse(xml);
        var itunesNamespace = XNamespace.Get(RssXmlNamespaces.Itunes);
        var itunesSeason = doc.Descendants(itunesNamespace + "season").FirstOrDefault();
        itunesSeason.Should().BeNull("ItunesSeason property was removed from RssItem");
    }
}
