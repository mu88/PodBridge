using PodBridge.Logic.Config;
using PodBridge.Logic.Domain;

namespace PodBridge.Logic.EpisodeSourcing;

internal interface IEpisodeSource
{
    Task<Podcast> FetchEpisodesAsync(PodcastConfig podcast, CancellationToken cancellationToken);
}
