namespace PodBridge.Logic.Shared;

public sealed record Podcast(
    string Title,
    string? Description,
    Uri? ImageUrl,
    IReadOnlyList<Episode> Episodes,
    string? Language = null,
    string? Author = null,
    Uri? Link = null);
