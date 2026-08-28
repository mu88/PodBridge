using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace PodBridge.Api.Rss;

public static class RssFeedSerializer
{
    /// <summary>
    /// Serializes the given feed to RSS/XML. The cached <paramref name="feed"/> instance is never mutated:
    /// when <paramref name="selfLinkUrl"/> is provided, a non-destructive copy carrying the atom:link self
    /// reference is produced via record <c>with</c>-expressions before serialization.
    /// </summary>
    public static string Serialize(RssFeed feed, string? selfLinkUrl = null)
    {
        ArgumentNullException.ThrowIfNull(feed);

        var feedToSerialize = selfLinkUrl is null
            ? feed
            : feed with { Channel = feed.Channel with { AtomSelfLink = new AtomLink { Href = selfLinkUrl } } };

        using var stringWriter = new Utf8StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",

            // Fixed "\n" (not Environment.NewLine) for deterministic, platform-independent output: this is
            // HTTP response content, not a local file/log, so it must not vary between Windows dev and Linux Docker.
            NewLineChars = "\n",
            Encoding = Encoding.UTF8,
            ConformanceLevel = ConformanceLevel.Document,
        });

        var serializer = new XmlSerializer(typeof(RssFeed));
        var namespaces = new XmlSerializerNamespaces();
        namespaces.Add("itunes", RssXmlNamespaces.Itunes);
        serializer.Serialize(xmlWriter, feedToSerialize, namespaces);

        var xml = stringWriter.ToString();
        return xml.TrimEnd() + "\n";
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
