using PodBridge.Logic.Config;

namespace Tests.TestSupport.Builders;

public sealed class PodBridgeOptionsBuilder
{
    private readonly List<PodcastConfig> _podcasts = [];
    private string? _pathBase;
    private int _refreshIntervalMinutes = 60;
    private int _rateLimitingPermitLimit = 15;
    private int _rateLimitingWindowMinutes = 5;
    private Uri? _graphQlEndpoint;
    private bool _authEnabled;
    private string? _authUsernameHash;
    private string? _authPasswordHash;

    public PodBridgeOptionsBuilder WithDefaults()
    {
        _pathBase = null;
        _refreshIntervalMinutes = 60;
        _rateLimitingPermitLimit = 15;
        _rateLimitingWindowMinutes = 5;
        _graphQlEndpoint = new Uri("https://fixture.test/graphql");
        _authEnabled = false;
        _authUsernameHash = null;
        _authPasswordHash = null;
        return this;
    }

    public PodBridgeOptionsBuilder WithPodcast(PodcastConfig podcast)
    {
        _podcasts.Add(podcast);
        return this;
    }

    public PodBridgeOptionsBuilder WithPodcast(PodcastConfigBuilder podcastBuilder)
    {
        _podcasts.Add(podcastBuilder.Build());
        return this;
    }

    public PodBridgeOptionsBuilder WithGraphQlEndpoint(Uri? endpoint)
    {
        _graphQlEndpoint = endpoint;
        return this;
    }

    public PodBridgeOptionsBuilder WithAuth(bool enabled, string? usernameHash = null, string? passwordHash = null)
    {
        _authEnabled = enabled;
        _authUsernameHash = usernameHash;
        _authPasswordHash = passwordHash;
        return this;
    }

    public PodBridgeOptions Build()
    {
        return new PodBridgeOptions
        {
            Podcasts = _podcasts,
            PathBase = _pathBase,
            RefreshIntervalMinutes = _refreshIntervalMinutes,
            RateLimitingPermitLimit = _rateLimitingPermitLimit,
            RateLimitingWindowMinutes = _rateLimitingWindowMinutes,
            GraphQlEndpoint = _graphQlEndpoint,
            Auth = new AuthOptions
            {
                Enabled = _authEnabled,
                UsernameHash = _authUsernameHash ?? string.Empty,
                PasswordHash = _authPasswordHash ?? string.Empty,
            },
        };
    }
}
