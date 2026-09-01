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
