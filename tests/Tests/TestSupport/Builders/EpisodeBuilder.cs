using PodBridge.Logic.Domain;

namespace Tests.TestSupport.Builders;

public sealed class EpisodeBuilder
{
    private string _guid = string.Empty;
    private string _title = string.Empty;
    private string? _description;
    private DateTimeOffset _publishDate = DateTimeOffset.MinValue;
    private Uri? _audioUrl;
    private int? _durationSeconds;
    private Uri? _imageUrl;
    private string? _episodeNumber;
    private Uri? _link;
    private string _audioMimeType = "audio/mpeg";

    public EpisodeBuilder WithDefaults()
    {
        _guid = $"fixture-guid-{Guid.NewGuid():N}";
        _title = "Fixture Episode";
        _description = "Fixture episode description";
        _publishDate = new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);
        _audioUrl = new Uri("https://fixture.test/audio.mp3");
        _durationSeconds = 1800;
        _imageUrl = new Uri("https://fixture.test/episode-image.jpg");
        _episodeNumber = "1";
        _link = new Uri("https://fixture.test/episode-page");
        _audioMimeType = "audio/mpeg";
        return this;
    }

    public EpisodeBuilder WithMinimalMetadata()
    {
        _description = null;
        _durationSeconds = null;
        _imageUrl = null;
        _episodeNumber = null;
        _link = null;
        return this;
    }

    public Episode Build()
    {
        return new Episode(
            Guid: _guid,
            Title: _title,
            Description: _description,
            PublishDate: _publishDate,
            AudioUrl: _audioUrl!,
            DurationSeconds: _durationSeconds,
            ImageUrl: _imageUrl,
            EpisodeNumber: _episodeNumber,
            Link: _link,
            AudioMimeType: _audioMimeType);
    }
}
