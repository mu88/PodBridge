using System.ComponentModel.DataAnnotations;

namespace PodBridge.Logic.Config;

public sealed class PodBridgeOptions : IValidatableObject
{
    public const string SectionName = "PodBridge";

    // Unlike the other properties here, this one is only read once, at worker startup, to construct a
    // PeriodicTimer with a fixed period - changing it in a reloaded config file has no effect until the
    // process is restarted.
    //
    // Superseded by RefreshInterval (sub-minute granularity); kept only so a rolling deployment can run
    // old and new config simultaneously. Remove this property (and its appsettings.json/README entries)
    // in a later, separate deployment once all environments have migrated to RefreshInterval.
    [Range(1, int.MaxValue)]
    public int RefreshIntervalMinutes { get; init; } = 360;

    // TimeSpan-based successor to RefreshIntervalMinutes, allowing sub-minute intervals (e.g. for fast
    // System/E2E tests). Takes precedence over RefreshIntervalMinutes when set - see EffectiveRefreshInterval.
    public TimeSpan? RefreshInterval { get; init; }

    // Single source of truth for the worker's actual refresh period, resolving the RefreshInterval vs.
    // RefreshIntervalMinutes precedence in one place instead of duplicating the fallback wherever the
    // period is needed.
    public TimeSpan EffectiveRefreshInterval => RefreshInterval ?? TimeSpan.FromMinutes(RefreshIntervalMinutes);

    // Defaults to true so existing deployments keep refreshing without any config change; tests disable it
    // (see TestWebApplicationFactory) so per-test hosts don't run an unnecessary background loop.
    public bool BackgroundRefreshEnabled { get; init; } = true;

    [Range(1, 100)]
    public int RateLimitingPermitLimit { get; init; } = 15;

    [Range(1, 60)]
    public int RateLimitingWindowMinutes { get; init; } = 5;

    public Uri? GraphQlEndpoint { get; init; }

    public AuthOptions Auth { get; init; } = new();

    // Podcasts themselves are runtime-mutable, user-editable data, not deployment/infra configuration, so
    // they live in a dedicated Postgres-backed AppDbContext/DbSet<PodcastConfig> instead (see
    // PodBridge.Persistence.AppDbContext), including their uniqueness/non-empty invariants (see
    // AppDbContext.OnModelCreating).
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (HasInvalidAuthConfiguration())
        {
            yield return new ValidationResult("Auth.UsernameHash and Auth.PasswordHash must be set when Auth.Enabled is true");
        }

        if (GraphQlEndpoint is not null && !GraphQlEndpoint.IsAbsoluteUri)
        {
            yield return new ValidationResult("GraphQlEndpoint must be an absolute URI");
        }

        if (RefreshInterval <= TimeSpan.Zero)
        {
            yield return new ValidationResult("RefreshInterval must be greater than zero");
        }
    }

    private bool HasInvalidAuthConfiguration()
    {
        return Auth.Enabled &&
               (string.IsNullOrWhiteSpace(Auth.UsernameHash) || string.IsNullOrWhiteSpace(Auth.PasswordHash));
    }
}
