namespace PodBridge.Logic.Domain;

public sealed record Podcast(
    string Title,
    string? Description,
    Uri? ImageUrl,
    IReadOnlyList<Episode> Episodes,
    string? Language = null,
    string? Author = null,
    Uri? Link = null);
