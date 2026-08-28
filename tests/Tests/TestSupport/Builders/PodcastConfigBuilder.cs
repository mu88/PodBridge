using PodBridge.Logic.Config;

namespace Tests.TestSupport.Builders;

public sealed class PodcastConfigBuilder
{
    private string _podcastId = string.Empty;
    private string _showId = string.Empty;

    public PodcastConfigBuilder WithDefaults()
    {
        _podcastId = $"fixture-podcast-{Guid.NewGuid():N}";
        _showId = $"fixture-show-{Guid.NewGuid():N}";
        return this;
    }

    public PodcastConfigBuilder WithPodcastId(string podcastId)
    {
        _podcastId = podcastId;
        return this;
    }

    public PodcastConfigBuilder WithShowId(string showId)
    {
        _showId = showId;
        return this;
    }

    public PodcastConfig Build()
    {
        return new PodcastConfig(_podcastId, _showId);
    }
}
