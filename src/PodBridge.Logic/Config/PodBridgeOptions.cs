using System.ComponentModel.DataAnnotations;

namespace PodBridge.Logic.Config;

public sealed class PodBridgeOptions : IValidatableObject
{
    public const string SectionName = "PodBridge";

    [Range(1, int.MaxValue)]
    public int RefreshIntervalMinutes { get; init; } = 360;

    // Defaults to true so existing deployments keep refreshing without any config change; tests disable it
    // (see TestWebApplicationFactory) so per-test hosts don't run an unnecessary background loop.
    public bool BackgroundRefreshEnabled { get; init; } = true;

    [Range(1, 100)]
    public int RateLimitingPermitLimit { get; init; } = 15;

    [Range(1, 60)]
    public int RateLimitingWindowMinutes { get; init; } = 5;

    public Uri? GraphQlEndpoint { get; init; }

    public AuthOptions Auth { get; init; } = new();

    public IReadOnlyList<PodcastConfig> Podcasts { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var podcasts = Podcasts.ToList();

        if (HasDuplicatePodcastIds(podcasts))
        {
            yield return new ValidationResult("PodcastIds must be unique");
        }

        if (HasEmptyPodcastId(podcasts))
        {
            yield return new ValidationResult("Every podcast must have a non-empty PodcastId");
        }

        if (HasEmptyShowId(podcasts))
        {
            yield return new ValidationResult("Every podcast must have a non-empty ShowId");
        }

        if (HasDuplicateShowIds(podcasts))
        {
            yield return new ValidationResult("Podcast ShowIds must be unique");
        }

        if (HasInvalidAuthConfiguration())
        {
            yield return new ValidationResult("Auth.UsernameHash and Auth.PasswordHash must be set when Auth.Enabled is true");
        }

        if (GraphQlEndpoint is not null && !GraphQlEndpoint.IsAbsoluteUri)
        {
            yield return new ValidationResult("GraphQlEndpoint must be an absolute URI");
        }

        if (podcasts.Count > 0 && GraphQlEndpoint is null)
        {
            yield return new ValidationResult("GraphQlEndpoint must be configured when podcasts are enabled");
        }
    }

    private static bool HasDuplicatePodcastIds(List<PodcastConfig> podcasts)
    {
        var seenIds = new HashSet<string>(podcasts.Count, StringComparer.Ordinal);
        return podcasts.Exists(podcast => !seenIds.Add(podcast.PodcastId));
    }

    private static bool HasEmptyPodcastId(IEnumerable<PodcastConfig> podcasts)
    {
        return podcasts.Any(podcast => string.IsNullOrWhiteSpace(podcast.PodcastId));
    }

    private static bool HasEmptyShowId(IEnumerable<PodcastConfig> podcasts)
    {
        return podcasts.Any(podcast => string.IsNullOrWhiteSpace(podcast.ShowId));
    }

    private static bool HasDuplicateShowIds(List<PodcastConfig> podcasts)
    {
        var seenIds = new HashSet<string>(podcasts.Count, StringComparer.Ordinal);
        return podcasts.Exists(podcast => !seenIds.Add(podcast.ShowId));
    }

    private bool HasInvalidAuthConfiguration()
    {
        return Auth.Enabled &&
               (string.IsNullOrWhiteSpace(Auth.UsernameHash) || string.IsNullOrWhiteSpace(Auth.PasswordHash));
    }
}
