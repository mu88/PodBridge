namespace PodBridge.Logic.Domain;

public sealed record Episode(
    string Guid,
    string Title,
    string? Description,
    DateTimeOffset PublishDate,
    Uri AudioUrl,
    int? DurationSeconds = null,
    Uri? ImageUrl = null,
    string? EpisodeNumber = null,
    Uri? Link = null,
    string AudioMimeType = "audio/mpeg");
