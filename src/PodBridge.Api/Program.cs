// Stryker disable all : Program.cs is the ASP.NET Core composition root; DI wiring and middleware configuration mutations are not meaningful at unit level
using System.Diagnostics.CodeAnalysis;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using mu88.Shared.OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using PodBridge.Api;
using PodBridge.Api.Authentication;
using PodBridge.Api.Components;
using PodBridge.Api.Components.Pages;
using PodBridge.Api.Endpoints;
using PodBridge.Logic;
using PodBridge.Logic.Config;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddKeyPerFile("/run/secrets", optional: true);

// Allows the full PodBridge configuration section (including the Podcasts array, which is unwieldy to set
// via individual environment variables) to be provided as a single file mounted into the container - e.g.
// via a Hostim.dev Volume. Read as a raw environment variable (not through builder.Configuration) so tests
// can point it at a temp file deterministically before the host is built. Silently has no effect (optional:
// true) when the file doesn't exist, so this is a no-op for local development and other deployments.
var externalConfigFilePath = Environment.GetEnvironmentVariable("PODBRIDGE_EXTERNAL_CONFIG_FILE_PATH") ?? "/data/podbridge.appsettings.json";
builder.Configuration.AddJsonFile(externalConfigFilePath, optional: true, reloadOnChange: true);

builder.Services.ConfigureOpenTelemetry("podbridge", builder.Configuration);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Observability.Source.Name))
    .WithMetrics(metrics => metrics.AddMeter(Observability.MeterName));

builder.Services.AddHealthChecks();
builder.Services.Configure<HealthCheckPublisherOptions>(options => options.Period = TimeSpan.FromMinutes(1));
builder.Services.AddHttpContextAccessor();
builder.Services.RegisterPodBridgeServices(builder.Configuration);
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
        var options = context.RequestServices.GetRequiredService<IOptionsMonitor<PodBridgeOptions>>().CurrentValue;
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
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
builder.Services.AddRazorComponents();
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
if (!string.IsNullOrWhiteSpace(resolvedOptions.PathBase))
{
    app.UsePathBase(resolvedOptions.PathBase);
}

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
app.MapScalarApiReference("/scalar", (options, httpContext) =>
{
    var pathBase = httpContext.Request.PathBase.Value;
    var openApiRoutePattern = string.IsNullOrEmpty(pathBase)
        ? "/openapi/{documentName}.json"
        : $"{pathBase}/openapi/{{documentName}}.json";

    options.WithTitle("PodBridge API Reference")
        .WithOpenApiRoutePattern(openApiRoutePattern)
        .AddDocument("v1", "PodBridge API")
        .AddPreferredSecuritySchemes("basicAuth")
        .DisableAgent();
});

protectedApiEndpoints.MapPodcastEndpoints();
protectedUiEndpoints.MapRazorComponents<App>();

// Renders the Blazor NotFound page for requests that never match any endpoint (e.g. a mistyped URL).
// The Blazor Router's own NotFound handling only covers NavigationManager.NotFound() calls from
// already-routed components, not requests that never reach the Blazor pipeline in the first place.
app.MapFallback(() => new RazorComponentResult<NotFoundPage>() { StatusCode = StatusCodes.Status404NotFound });

await app.RunAsync();

[ExcludeFromCodeCoverage(Justification = "Composition root; excluded from Sonar coverage metric too (see SonarQube.Analysis.xml).")]
[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "S1118", Justification = "Necessary for code coverage")]
[SuppressMessage("ASP", "ASP0027:Using public partial class Program is no longer required", Justification = "StyleCop SA1205 requires access modifier on partial types")]
public partial class Program;
