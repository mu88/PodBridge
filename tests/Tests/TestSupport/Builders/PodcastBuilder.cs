using PodBridge.Logic.Domain;

namespace Tests.TestSupport.Builders;

public sealed class PodcastBuilder
{
    private string _title = string.Empty;
    private string? _description;
    private Uri? _imageUrl;
    private List<Episode> _episodes = [];
    private string? _language;
    private string? _author;
    private Uri? _link;

    public PodcastBuilder WithDefaults()
    {
        _title = "Fixture Podcast";
        _description = "Fixture podcast description";
        _imageUrl = new Uri("https://fixture.test/podcast-image.jpg");
        _episodes = [];
        _language = "en";
        _author = "Fixture Author";
        _link = new Uri("https://fixture.test/podcast-page");
        return this;
    }

    public PodcastBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public PodcastBuilder WithEpisodes(params Episode[] episodes)
    {
        _episodes = episodes.ToList();
        return this;
    }

    public Podcast Build()
    {
        return new Podcast(
            Title: _title,
            Description: _description,
            ImageUrl: _imageUrl,
            Episodes: _episodes,
            Language: _language,
            Author: _author,
            Link: _link);
    }
}
