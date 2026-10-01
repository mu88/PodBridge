using PodBridge.Logic.Config;
using PodBridge.Logic.Shared;

namespace PodBridge.Logic.EpisodeSourcing;

internal interface IEpisodeSource
{
    Task<Podcast> FetchEpisodesAsync(PodcastConfig podcast, CancellationToken cancellationToken);
}
