// Stryker disable all : Program.cs is the ASP.NET Core composition root; DI wiring and middleware configuration mutations are not meaningful at unit level
using System.Diagnostics.CodeAnalysis;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using mu88.Shared.OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PodBridge.Api;
using PodBridge.Api.Authentication;
using PodBridge.Api.Components;
using PodBridge.Api.Components.Pages;
using PodBridge.Api.Endpoints;
using PodBridge.Api.Observability;
using PodBridge.Api.Refresh;
using PodBridge.Logic;
using PodBridge.Logic.Config;
using PodBridge.Logic.Versioning;
using PodBridge.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddKeyPerFile("/run/secrets", optional: true);

// Allows the full PodBridge configuration section (including the Podcasts array, which is unwieldy to set
// via individual environment variables) to be provided as a single file mounted into the container - e.g.
// via a managed volume mount on the hosting platform. Only attempted when PODBRIDGE_EXTERNAL_CONFIG_FILE_PATH
// is explicitly set, so there's no hardcoded default path that could point at a directory that doesn't exist
// (e.g. an unmounted volume) - reloadOnChange: true on such a path makes .NET's FileSystemWatcher fall back to
// recursively watching the nearest existing ancestor directory instead, which can take minutes on a large
// filesystem and was blocking host startup (verified via a CI hang-dump/CLR stack trace showing
// FileSystemWatcher.StartRaisingEvents -> AddDirectoryWatchUnlocked stuck enumerating directories). Read as
// a raw environment variable (not through builder.Configuration) so tests can point it at a temp file
// deterministically before the host is built.
var externalConfigFilePath = Environment.GetEnvironmentVariable("PODBRIDGE_EXTERNAL_CONFIG_FILE_PATH");
if (!string.IsNullOrWhiteSpace(externalConfigFilePath))
{
    // Defense-in-depth for the same FileSystemWatcher fallback described above, in case an operator sets
    // the environment variable to a path whose directory isn't mounted yet.
    var externalConfigDirectoryExists = Directory.Exists(Path.GetDirectoryName(externalConfigFilePath));
    builder.Configuration.AddJsonFile(externalConfigFilePath, optional: true, reloadOnChange: externalConfigDirectoryExists);
}

builder.Services.ConfigureOpenTelemetry("podbridge", builder.Configuration);

// AppVersionResourceDetector resolves IAppVersionProvider via the factory's IServiceProvider, which is
// only invoked once the TracerProvider/MeterProvider is actually built (after the real DI container
// exists) - so the app's DI-registered IAppVersionProvider can be reused here instead of constructing
// a second, separate instance before the container is available.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddDetector(sp => new AppVersionResourceDetector(sp.GetRequiredService<IAppVersionProvider>())))
    .WithTracing(tracing => tracing.AddSource(Observability.Source.Name))
    .WithMetrics(metrics => metrics.AddMeter(Observability.MeterName));

builder.Services.AddHealthChecks()
    .AddCheck<EpisodeRefreshHealthCheck>("episode-refresh");
builder.Services.Configure<HealthCheckPublisherOptions>(options => options.Period = TimeSpan.FromMinutes(1));
builder.Services.AddHttpContextAccessor();
builder.Services.RegisterPodBridgeServices(builder.Configuration);
builder.Services.RegisterPodBridgePersistenceServices(builder.Configuration);
builder.Services.AddPodBridgeAuthentication();
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rateLimiterOptions.AddPolicy("feed-endpoint", CreateRateLimitPartition);
    rateLimiterOptions.AddPolicy("podcasts-endpoint", CreateRateLimitPartition);
    rateLimiterOptions.AddPolicy("login-endpoint", CreateLoginRateLimitPartition);

    static RateLimitPartition<string> CreateRateLimitPartition(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptionsMonitor<PodBridgeOptions>>().CurrentValue;
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.RateLimitingPermitLimit,
                Window = TimeSpan.FromMinutes(options.RateLimitingWindowMinutes),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
    }

    // Separate from CreateRateLimitPartition/RateLimitingPermitLimit above: brute-force login
    // protection needs a much stricter threshold than legitimate podcatcher API polling, so the
    // login endpoint gets its own, independently configurable rate limit (see AuthOptions).
    static RateLimitPartition<string> CreateLoginRateLimitPartition(HttpContext context)
    {
        var ipKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // The "/login" route is hit by both the initial GET page view (and every browser refresh or
        // redirect back after logout) and the POST form submission, since Blazor's enhanced navigation
        // posts back to the same route. Only POST represents an actual login attempt - counting GET
        // requests too would lock out a legitimate user just for viewing the page a few times, without
        // ever having submitted a single credential. The partition key must differ per method: the
        // PartitionedRateLimiter caches the first limiter created for a given key and ignores the
        // RateLimitPartition returned by later calls with that same key, so reusing "ipKey" for both
        // GET and POST would make whichever method hits first "win" for the whole window.
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter($"{ipKey}-get");
        }

        var options = context.RequestServices.GetRequiredService<IOptionsMonitor<PodBridgeOptions>>().CurrentValue;
        return RateLimitPartition.GetFixedWindowLimiter(
            $"{ipKey}-post",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.Auth.RateLimitingPermitLimit,
                Window = TimeSpan.FromMinutes(options.Auth.RateLimitingWindowMinutes),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
    }
});

builder.Services.AddAntiforgery(options => options.Cookie.Path = "/");
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddOpenApi("v1", options =>
{
    options.ShouldInclude = description =>
        description.RelativePath is not null &&
        description.RelativePath.StartsWith("api/", StringComparison.OrdinalIgnoreCase);

    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "PodBridge API",
            Version = "v1",
            Description = "RSS and JSON endpoints for configured podcasts.",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal)
        {
            ["basicAuth"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "basic",
                Description = "Enter the same username and password that podcatchers use for /api requests.",
            },
        };

        foreach (var operation in document.Paths.Values.SelectMany(pathItem => pathItem.Operations!.Values))
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("basicAuth", document)] = [],
            });
        }

        return Task.CompletedTask;
    });
});

var app = builder.Build();

var resolvedOptions = app.Services.GetRequiredService<IOptions<PodBridgeOptions>>().Value;

// Applies pending EF Core migrations before the app starts serving traffic. Retried with backoff (not
// a single attempt) because on a fresh deployment the Postgres container/managed instance may not accept
// connections yet by the time this app's process starts; failures are logged and swallowed rather than
// crashing the whole host, since the rest of PodBridge (already-cached feeds, Auth, etc.) still works
// without a reachable database - PodcastDatabaseHealthCheck surfaces the resulting Degraded status instead.
await MigrateDatabaseWithRetryAsync(app.Services);

// TEMPORARY (remove after the one-time production deploy that runs this): migrates podcasts that used to
// be configured via the "PodBridge:Podcasts" appsettings/env-var section (see git history of
// PodBridgeOptions) into the new Postgres-backed AppDbContext.Podcasts table. Reads the legacy section
// directly via IConfiguration (not through PodBridgeOptions, which no longer has a Podcasts property at
// all) so this works purely from raw config without any dependency on the removed binding. Fail-safe/
// idempotent: only inserts podcasts whose ShowId isn't already present, so re-running this on every
// startup after the legacy section has been removed - or if it's re-run against an already-migrated
// database - is always a safe no-op.
await MigrateLegacyPodcastConfigAsync(app.Services, builder.Configuration);

// Trust the immediate reverse proxy (e.g. a managed container platform's built-in front-end, or an
// operator-provided nginx/Traefik/Caddy) so RemoteIpAddress - used by the rate limiter below - reflects
// the real client IP instead of the proxy's. KnownNetworks/KnownProxies are cleared because such proxies
// typically run on a private container network, not loopback (ASP.NET Core's default trusted range).
// This is safe only because PodBridge is documented as private/non-public use: it must not be reachable
// except through that trusted proxy, otherwise a direct caller could spoof X-Forwarded-For.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseSecurityHeaders();

app.UseRouting();

// UseRateLimiter must run after UseRouting so endpoint metadata (RequireRateLimiting) is available.
app.UseRateLimiter();

app.UsePodBridgeAuthentication(resolvedOptions.Auth.Enabled);
app.UseAntiforgery();

app.MapPodBridgeLogoutEndpoint(resolvedOptions);

// UI (Blazor pages/static assets) stays at the root so the dashboard is reachable at the root path,
// while the JSON/RSS API is grouped under /api to keep the two surfaces unambiguous.
var protectedUiEndpoints = app.MapPodBridgeProtectedGroup(string.Empty, PodBridgeAuthorizationPolicies.Ui, resolvedOptions.Auth.Enabled);
var protectedApiEndpoints = app.MapPodBridgeProtectedGroup("/api", PodBridgeAuthorizationPolicies.Api, resolvedOptions.Auth.Enabled);

app.MapStaticAssets();
app.MapHealthChecks("/healthz");
app.MapOpenApi("/openapi/{documentName}.json");
app.MapScalarApiReference("/scalar", options =>
{
    options.WithTitle("PodBridge API Reference")
        .WithOpenApiRoutePattern("/openapi/{documentName}.json")
        .AddDocument("v1", "PodBridge API")
        .AddPreferredSecuritySchemes("basicAuth")
        .DisableAgent();
});

protectedApiEndpoints.MapPodcastEndpoints();
protectedApiEndpoints.MapPodcastManagementEndpoints();
protectedUiEndpoints.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// Renders the Blazor NotFound page for requests that never match any endpoint (e.g. a mistyped URL).
// The Blazor Router's own NotFound handling only covers NavigationManager.NotFound() calls from
// already-routed components, not requests that never reach the Blazor pipeline in the first place.
app.MapFallback(() => new RazorComponentResult<NotFoundPage>() { StatusCode = StatusCodes.Status404NotFound });

await app.RunAsync();

// Isolated as a local, non-async-Main-body function so the retry loop is testable in isolation and to
// keep the top-level statements above focused on host/middleware wiring.
static async Task MigrateDatabaseWithRetryAsync(IServiceProvider services)
{
    const int maxAttempts = 5;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("PodBridge.Startup.Migrations");

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            // Migrations are Npgsql-specific; applying them against another provider (e.g. integration tests'
            // SQLite) always fails EF Core's PendingModelChangesWarning check, so skip - those tests seed their
            // own schema via EnsureCreated instead (see SqliteAppDbContextFactoryScope).
            if (!dbContext.Database.IsNpgsql())
            {
                return;
            }

            await dbContext.Database.MigrateAsync();
            return;
        }
        catch (Exception exception) when (attempt < maxAttempts)
        {
            logger.LogWarning(
                exception,
                "Database migration attempt {Attempt}/{MaxAttempts} failed; retrying in {DelaySeconds}s",
                attempt,
                maxAttempts,
                attempt);
            await Task.Delay(TimeSpan.FromSeconds(attempt));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Database migration failed after {MaxAttempts} attempts; continuing startup in a degraded state", maxAttempts);
        }
    }
}

// TEMPORARY: see call site comment above for context/removal plan.
static async Task MigrateLegacyPodcastConfigAsync(IServiceProvider services, IConfiguration configuration)
{
    var legacyPodcasts = configuration.GetSection("PodBridge:Podcasts").Get<List<PodcastConfig>>() ?? [];
    if (legacyPodcasts.Count == 0)
    {
        return;
    }

    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("PodBridge.Startup.LegacyPodcastMigration");

    try
    {
        await using var scope = services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var existingShowIds = await dbContext.Podcasts.Select(podcast => podcast.ShowId).ToListAsync();
        var podcastsToMigrate = legacyPodcasts.Where(podcast => !existingShowIds.Contains(podcast.ShowId, StringComparer.Ordinal)).ToList();
        if (podcastsToMigrate.Count == 0)
        {
            return;
        }

        dbContext.Podcasts.AddRange(podcastsToMigrate);
        await dbContext.SaveChangesAsync();

        // .Count on a List<T> is a trivial O(1) read, not an expensive log argument.
#pragma warning disable CA1873
        logger.LogInformation("Migrated {Count} legacy podcast(s) from configuration into the database", podcastsToMigrate.Count);
#pragma warning restore CA1873
    }
    catch (Exception exception)
    {
        // Non-fatal: the legacy section stays in config until the operator removes it, so a failed
        // attempt here (e.g. a still-unreachable database) is simply retried on the next app restart.
        logger.LogError(exception, "Failed to migrate legacy podcast configuration into the database");
    }
}

[ExcludeFromCodeCoverage(Justification = "Composition root; excluded from Sonar coverage metric too (see SonarQube.Analysis.xml).")]
[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "S1118", Justification = "Necessary for code coverage")]
[SuppressMessage("ASP", "ASP0027:Using public partial class Program is no longer required", Justification = "StyleCop SA1205 requires access modifier on partial types")]
public partial class Program;
