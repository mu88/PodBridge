namespace PodBridge.Logic.EpisodeSourcing;

/// <summary>
/// A cheap, metadata-only preview of a show (title/cover), fetched before adding a podcast so the UI can
/// show a confirmation without first paying the cost of a full episode backfill (up to 5000 items).
/// </summary>
public sealed record PodcastPreview(string Title, Uri? ImageUrl);
