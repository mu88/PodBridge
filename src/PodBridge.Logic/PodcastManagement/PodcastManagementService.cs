using System.Globalization;
using Microsoft.Extensions.Logging;
using PodBridge.Logic.Config;
using PodBridge.Logic.EpisodeSourcing;
using PodBridge.Logic.Refresh;
using PodBridge.Logic.Shared;

namespace PodBridge.Logic.PodcastManagement;

internal sealed partial class PodcastManagementService(
    IPodcastDirectory podcastDirectory,
    IPodcastRepository podcastRepository,
    IPodcastRefreshService podcastRefreshService,
    ILogger<PodcastManagementService> logger) : IPodcastManagementService
{
    public async Task<PodcastAddResult> AddByShowIdAsync(string showId, CancellationToken cancellationToken)
    {
        PodcastPreview? preview;
        try
        {
            preview = await podcastDirectory.FindPreviewAsync(showId, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            LogFailedToResolveShowId(exception, showId);
            return new PodcastAddResult(PodcastAddStatus.Failed);
        }

        return preview is null
            ? new PodcastAddResult(PodcastAddStatus.ShowIdNotFound)
            : await AddAsync(showId, preview.Title, cancellationToken);
    }

    public async Task<PodcastAddResult> AddAsync(string showId, string title, CancellationToken cancellationToken)
    {
        var podcastId = await GenerateUniquePodcastIdAsync(title, cancellationToken);
        var podcast = new PodcastConfig(podcastId, showId);

        var outcome = await podcastRepository.AddAsync(podcast, cancellationToken);
        switch (outcome)
        {
            case PodcastRepositoryAddOutcome.ShowIdAlreadyExists:
                return new PodcastAddResult(PodcastAddStatus.ShowIdAlreadyExists, title);
            case PodcastRepositoryAddOutcome.Failed:
                LogFailedToAddPodcast(showId);
                return new PodcastAddResult(PodcastAddStatus.Failed, title);
            case PodcastRepositoryAddOutcome.Added:
            default:
                break;
        }

        // Fire-and-forget: the potentially large episode backfill (up to 5000 items) must not block the
        // caller (UI request or API request) - the caller gets an immediate confirmation, and the podcast
        // simply shows as "not yet fetched" until the periodic refresh worker (or this task) completes
        // the first refresh.
        _ = podcastRefreshService.RefreshOneAsync(podcast, CancellationToken.None);

        return new PodcastAddResult(PodcastAddStatus.Added, title, podcastId);
    }

    private static string Slugify(string title)
    {
        var slug = new string(title
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N") : slug;
    }

    // PodcastId doubles as the URL segment for this podcast's feed, so it's derived from the title as a
    // readable slug instead of e.g. a GUID - falls back to a generated GUID if the title yields no usable
    // characters at all (e.g. a title consisting only of emoji/symbols).
    private async Task<string> GenerateUniquePodcastIdAsync(string title, CancellationToken cancellationToken)
    {
        var baseSlug = Slugify(title);
        var candidate = baseSlug;
        var suffix = 2;

        while (await podcastRepository.ExistsWithPodcastIdAsync(candidate, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix.ToString(CultureInfo.InvariantCulture)}";
            suffix++;
        }

        return candidate;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to preview ShowId '{ShowId}'")]
    private partial void LogFailedToResolveShowId(Exception exception, string showId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to add podcast for ShowId '{ShowId}'")]
    private partial void LogFailedToAddPodcast(string showId);
}
