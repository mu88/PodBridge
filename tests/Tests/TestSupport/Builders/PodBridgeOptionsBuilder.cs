using PodBridge.Logic.Config;

namespace Tests.TestSupport.Builders;

public sealed class PodBridgeOptionsBuilder
{
    private TimeSpan _refreshInterval = TimeSpan.FromHours(1);
    private bool _backgroundRefreshEnabled = true;
    private int _rateLimitingPermitLimit = 15;
    private int _rateLimitingWindowMinutes = 5;
    private Uri? _graphQlEndpoint;
    private bool _authEnabled;
    private string? _authUsernameHash;
    private string? _authPasswordHash;

    public PodBridgeOptionsBuilder WithDefaults()
    {
        _refreshInterval = TimeSpan.FromHours(1);
        _backgroundRefreshEnabled = true;
        _rateLimitingPermitLimit = 15;
        _rateLimitingWindowMinutes = 5;
        _graphQlEndpoint = new Uri("https://fixture.test/graphql");
        _authEnabled = false;
        _authUsernameHash = null;
        _authPasswordHash = null;
        return this;
    }

    public PodBridgeOptionsBuilder WithRefreshInterval(TimeSpan interval)
    {
        _refreshInterval = interval;
        return this;
    }

    public PodBridgeOptionsBuilder WithBackgroundRefreshEnabled(bool enabled)
    {
        _backgroundRefreshEnabled = enabled;
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
            RefreshInterval = _refreshInterval,
            BackgroundRefreshEnabled = _backgroundRefreshEnabled,
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
