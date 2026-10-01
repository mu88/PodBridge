namespace PodBridge.Logic.EpisodeSourcing;

/// <summary>
/// A single show-title search hit, used to let a user pick a show to add without needing to know its raw
/// ShowId upfront.
/// </summary>
public sealed record PodcastSearchResult(string ShowId, string Title, int EpisodeCount);
