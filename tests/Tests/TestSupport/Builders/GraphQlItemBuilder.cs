using PodBridge.Logic.EpisodeSourcing;

namespace Tests.TestSupport.Builders;

internal sealed class GraphQlItemBuilder
{
    private Item _item = new();

    public GraphQlItemBuilder WithDefaults()
    {
        _item = new Item
        {
            Id = $"fixture-item-{Guid.NewGuid():N}",
            Title = "Fixture Episode",
            PublishDate = new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero),
            Synopsis = "Fixture episode synopsis",
            Summary = "Fixture episode summary",
            Description = "Fixture episode description",
            ShowNotes = "Fixture episode show notes",
            Duration = 1800,
            EpisodeNumber = 1,
            SharingUrl = "https://fixture.test/episode",
            Image = new ImageType { Url = "https://fixture.test/images/{width}/episode.jpg" },
            Audios = [new AssetType { Url = "https://fixture.test/audio.mp3", DownloadUrl = "https://fixture.test/download/audio.mp3", MimeType = "audio/mpeg" }],
        };
        return this;
    }

    public Item Build()
    {
        return _item;
    }
}
