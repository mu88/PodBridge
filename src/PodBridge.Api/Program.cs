// Stryker disable all : Program.cs is the ASP.NET Core composition root; DI wiring and middleware configuration mutations are not meaningful at unit level
using System.Diagnostics.CodeAnalysis;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using mu88.Shared.OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using PodBridge.Api;
using PodBridge.Api.Authentication;
using PodBridge.Api.Components;
using PodBridge.Api.Endpoints;
using PodBridge.Logic;
using PodBridge.Logic.Config;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddKeyPerFile("/run/secrets", optional: true);

builder.Services.ConfigureOpenTelemetry("podbridge", builder.Configuration);
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Observability.Source.Name))
    .WithMetrics(metrics => metrics.AddMeter(Observability.MeterName));

builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.RegisterPodBridgeServices(builder.Configuration);
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rateLimiterOptions.AddPolicy("feed-endpoint", CreateRateLimitPartition);
    rateLimiterOptions.AddPolicy("podcasts-endpoint", CreateRateLimitPartition);

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
});

builder.Services
    .AddAuthentication(BasicAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

builder.Services.AddAntiforgery(options => options.Cookie.Path = "/");
builder.Services.AddRazorComponents();

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

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");

    // img-src allows any https origin because podcast cover art is hotlinked from whatever
    // upstream host the configured GraphQL endpoint returns (not fixed to one domain).
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' https:";
    await next();
});

// Required so that requests for undefined routes (e.g. a mistyped URL) render the Blazor NotFound page
// instead of an empty 404 response. Router.NotFoundPage alone only handles NavigationManager.NotFound()
// calls from within already-routed components, not requests that never match any endpoint.
app.UseStatusCodePagesWithReExecute("/not-found");

app.UseRouting();

// UseRateLimiter must run after UseRouting so endpoint metadata (RequireRateLimiting) is available.
app.UseRateLimiter();

if (resolvedOptions.Auth.Enabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseAntiforgery();

// UI (Blazor pages/static assets) stays at the root so the dashboard is reachable at the root path,
// while the JSON/RSS API is grouped under /api to keep the two surfaces unambiguous.
RouteGroupBuilder CreateProtectedGroup(string prefix)
{
    return resolvedOptions.Auth.Enabled
        ? app.MapGroup(prefix).RequireAuthorization()
        : app.MapGroup(prefix);
}

var protectedEndpoints = CreateProtectedGroup(string.Empty);
var protectedApiEndpoints = CreateProtectedGroup("/api");

protectedEndpoints.MapStaticAssets();
app.MapHealthChecks("/healthz");

protectedApiEndpoints.MapPodcastEndpoints();

protectedEndpoints.MapRazorComponents<App>();

await app.RunAsync();

[ExcludeFromCodeCoverage(Justification = "Composition root; excluded from Sonar coverage metric too (see SonarQube.Analysis.xml).")]
[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "S1118", Justification = "Necessary for code coverage")]
[SuppressMessage("ASP", "ASP0027:Using public partial class Program is no longer required", Justification = "StyleCop SA1205 requires access modifier on partial types")]
public partial class Program;
