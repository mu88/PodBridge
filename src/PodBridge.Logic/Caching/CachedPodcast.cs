using PodBridge.Logic.Domain;

namespace PodBridge.Logic.Caching;

/// <summary>
/// A podcast together with the timestamp of its last successful cache refresh. <see cref="LastUpdated"/>
/// is a caching concern (not part of the domain model), so it lives here rather than on <see cref="Podcast"/>.
/// </summary>
public sealed record CachedPodcast(Podcast Podcast, DateTimeOffset LastUpdated);
