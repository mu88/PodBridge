using System.Globalization;
using System.Xml.Serialization;
using PodBridge.Logic.Domain;

namespace PodBridge.Api.Rss;

/// <summary>
/// XML namespace constants shared by the RSS/iTunes/Atom feed DTOs.
/// </summary>
internal static class RssXmlNamespaces
{
    public const string Itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";
    public const string Atom = "http://www.w3.org/2005/Atom";
    public const string RssMediaType = "application/rss+xml";
}

[XmlRoot("rss")]
public sealed record RssFeed
{
    [XmlAttribute("version")]
    public string Version { get; init; } = "2.0";

    [XmlElement("channel")]
    public RssChannel Channel { get; init; } = new();

    public static RssFeed MapFrom(Podcast podcast)
    {
        ArgumentNullException.ThrowIfNull(podcast);

        return new RssFeed
        {
            Channel = new RssChannel
            {
                Title = podcast.Title,
                Link = podcast.Link?.OriginalString ?? string.Empty,
                Description = podcast.Description ?? podcast.Title,
                Language = podcast.Language,
                Image = podcast.ImageUrl is not null
                    ? new RssImage { Href = podcast.ImageUrl.OriginalString }
                    : null,
                ItunesAuthor = podcast.Author ?? podcast.Title,
                ItunesType = "episodic",
                ItunesExplicit = "no",
                Items = podcast.Episodes
                    .OrderByDescending(episode => episode.PublishDate)
                    .Select(MapItem)
                    .ToList(),
            },
        };
    }

    private static RssItem MapItem(Episode episode)
    {
        return new RssItem
        {
            Title = episode.Title,
            Guid = episode.Guid,
            PubDate = episode.PublishDate.UtcDateTime.ToString("R", CultureInfo.InvariantCulture),
            Description = episode.Description,
            Link = episode.Link?.OriginalString,
            Enclosure = new RssEnclosure
            {
                Url = episode.AudioUrl.OriginalString,
                Type = episode.AudioMimeType,
            },
            ItunesImage = episode.ImageUrl is null
                ? null
                : new RssItunesImage
                {
                    Href = episode.ImageUrl.OriginalString,
                },
            ItunesDuration = episode.DurationSeconds?.ToString(CultureInfo.InvariantCulture),
            ItunesEpisode = episode.EpisodeNumber,
        };
    }
}

public sealed record RssChannel
{
    [XmlElement("title")]
    public string Title { get; init; } = string.Empty;

    [XmlElement("link")]
    public string Link { get; init; } = string.Empty;

    [XmlElement("description")]
    public string Description { get; init; } = string.Empty;

    [XmlElement("language")]
    public string? Language { get; init; }

    [XmlElement("image")]
    public RssImage? Image { get; init; }

    [XmlElement("author", Namespace = RssXmlNamespaces.Itunes)]
    public string ItunesAuthor { get; init; } = string.Empty;

    [XmlElement("type", Namespace = RssXmlNamespaces.Itunes)]
    public string ItunesType { get; init; } = "episodic";

    [XmlElement("explicit", Namespace = RssXmlNamespaces.Itunes)]
    public string ItunesExplicit { get; init; } = "no";

    [XmlElement("link", Namespace = RssXmlNamespaces.Atom)]
    public AtomLink? AtomSelfLink { get; init; }

    [XmlElement("item")]
    public List<RssItem> Items { get; init; } = [];
}

public sealed class RssItem
{
    [XmlElement("title")]
    public string Title { get; set; } = string.Empty;

    [XmlElement("guid")]
    public string Guid { get; set; } = string.Empty;

    [XmlElement("pubDate")]
    public string PubDate { get; set; } = string.Empty;

    [XmlElement("description")]
    public string? Description { get; set; }

    [XmlElement("link")]
    public string? Link { get; set; }

    [XmlElement("enclosure")]
    public RssEnclosure? Enclosure { get; set; }

    [XmlElement("image", Namespace = RssXmlNamespaces.Itunes)]
    public RssItunesImage? ItunesImage { get; set; }

    [XmlElement("duration", Namespace = RssXmlNamespaces.Itunes)]
    public string? ItunesDuration { get; set; }

    [XmlElement("episode", Namespace = RssXmlNamespaces.Itunes)]
    public string? ItunesEpisode { get; set; }
}

public sealed class RssEnclosure
{
    [XmlAttribute("url")]
    public string Url { get; set; } = string.Empty;

    [XmlAttribute("type")]
    public string Type { get; set; } = "audio/mpeg";
}

public sealed class RssImage
{
    [XmlAttribute("href")]
    public string Href { get; set; } = string.Empty;
}

public sealed class RssItunesImage
{
    [XmlAttribute("href")]
    public string Href { get; set; } = string.Empty;
}

public sealed class AtomLink
{
    [XmlAttribute("rel")]
    public string Rel { get; set; } = "self";

    [XmlAttribute("href")]
    public string Href { get; set; } = string.Empty;

    [XmlAttribute("type")]
    public string Type { get; set; } = RssXmlNamespaces.RssMediaType;
}
