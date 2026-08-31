# PodBridge

![Combined CI / Release](https://github.com/mu88/PodBridge/actions/workflows/CI_CD.yml/badge.svg)
![Mutation testing](https://github.com/mu88/PodBridge/actions/workflows/Mutation%20Testing.yml/badge.svg)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=mu88_PodBridge&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=mu88_PodBridge)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=mu88_PodBridge&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=mu88_PodBridge)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=mu88_PodBridge&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=mu88_PodBridge)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=mu88_PodBridge&metric=bugs)](https://sonarcloud.io/summary/new_code?id=mu88_PodBridge)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=mu88_PodBridge&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=mu88_PodBridge)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=mu88_PodBridge&metric=code_smells)](https://sonarcloud.io/summary/new_code?id=mu88_PodBridge)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=mu88_PodBridge&metric=coverage)](https://sonarcloud.io/summary/new_code?id=mu88_PodBridge)
[![Mutation testing badge](https://img.shields.io/endpoint?style=flat&url=https%3A%2F%2Fbadge-api.stryker-mutator.io%2Fgithub.com%2Fmu88%2FPodBridge%2Fmain)](https://dashboard.stryker-mutator.io/reports/github.com/mu88/PodBridge/main)

A podcast metadata bridge that fetches episode data from a configurable GraphQL endpoint and republishes it as standard RSS 2.0 + iTunes-compatible feeds. Designed for private, self-hosted podcast aggregation.

## Features

- Fetch episodes from a GraphQL API with cursor-based pagination
- Generate standard RSS 2.0 + iTunes namespace podcast feeds
- In-memory caching with background refresh worker
- Cache podcast metadata and render RSS on demand
- Podcast overview endpoint for feed discovery
- Fixed-window rate limiting on feed and overview endpoints
- Security headers for browser-facing responses
- Health check endpoint
- Docker-ready via .NET SDK container building tools (no Dockerfile)

## Quick Start

### Local Development

```bash
# Build
dotnet build

# Run
dotnet run --project src/PodBridge.Api

# Test
dotnet test
```

The API listens on `http://localhost:5000` by default.

### Configuration

Configure the GraphQL endpoint and podcasts in `appsettings.json` under the `PodBridge` section:

```json
{
  "PodBridge": {
    "RefreshIntervalMinutes": 360,
    "RateLimitingPermitLimit": 15,
    "RateLimitingWindowMinutes": 5,
    "PathBase": "",
    "GraphQlEndpoint": "https://example.org/graphql",
    "Auth": {
      "Enabled": false,
      "UsernameHash": null,
      "PasswordHash": null
    },
    "Podcasts": [
      {
        "PodcastId": "example-show",
        "ShowId": "urn:example:show:1234567890abcdef"
      },
      {
        "PodcastId": "example-show-2",
        "ShowId": "123456"
      }
    ]
  }
}
```

- `RefreshIntervalMinutes`: Background refresh interval for episodes and pre-generated feeds.
- `RateLimitingPermitLimit`: Maximum number of requests per remote IP and protected endpoint within the configured window. Default: `15`.
- `RateLimitingWindowMinutes`: Fixed-window length for rate limiting on `/api/podcasts/{podcastId}` and `/api/podcasts`. Default: `5`.
- `PathBase`: Optional application base path when hosted below the domain root.
- `GraphQlEndpoint`: Absolute URI of the GraphQL endpoint used for show and episode lookups.
- `Podcasts`: List of podcast feeds to expose.
- `Podcasts[*].PodcastId`: Internal feed slug used in `/api/podcasts/{podcastId}` and the `/podcasts/{podcastId}` overview page.
- `Podcasts[*].ShowId`: Upstream GraphQL show identifier. Plain numeric IDs and URN-style IDs are both supported.
- `Auth.Enabled`: Optional boolean flag to enable HTTP Basic Authentication (RFC 7617) on all endpoints except `/healthz`. Default: `false`.
- `Auth.UsernameHash`: PBKDF2 hash of the Basic Auth username (required if `Auth.Enabled` is true). Generate it with `Scripts/New-CredentialHash.ps1` - the plaintext username is never stored.
- `Auth.PasswordHash`: PBKDF2 hash of the Basic Auth password (required if `Auth.Enabled` is true). Generate it with `Scripts/New-CredentialHash.ps1` - the plaintext password is never stored.

#### Authentication

HTTP Basic Authentication is opt-in via the `Auth.Enabled` configuration flag. When enabled, all endpoints except `/healthz` require valid credentials.

Credentials are configured as PBKDF2 hashes, not plaintext, so the actual username/password never needs to
exist in configuration, a secret store, or a deployment platform's dashboard - only the account owner needs
to know them. Generate the hashes once with `Scripts/New-CredentialHash.ps1`:

```powershell
./Scripts/New-CredentialHash.ps1 -Value 'myuser'
./Scripts/New-CredentialHash.ps1 -Value 'mypassword'
```

**Configuration sources:**

- **Environment variables** (standard .NET configuration):
  - `PodBridge__Auth__Enabled=true`
  - `PodBridge__Auth__UsernameHash=<hash produced by New-CredentialHash.ps1>`
  - `PodBridge__Auth__PasswordHash=<hash produced by New-CredentialHash.ps1>`

- **Docker secrets** (recommended for containerized deployments):
  - Mount secret files at `/run/secrets/`:
    - `/run/secrets/PodBridge__Auth__UsernameHash` (file content = username hash)
    - `/run/secrets/PodBridge__Auth__PasswordHash` (file content = password hash)
    - `/run/secrets/PodBridge__Auth__Enabled` (file content = `true` or `false`)
  - Note: Double underscores (`__`) in file names are converted to `:` config-key delimiters by .NET's Key-Per-File configuration provider.

- **Local development**:
  - Use standard .NET configuration sources such as environment variables, `launchSettings.json`, or `dotnet user-secrets`.

**Client usage:**

Podcast clients that support HTTP Basic Authentication (e.g., AntennaPod, Pocket Casts) can subscribe directly with credentials embedded in the URL:

```
https://username:password@yourhost/api/podcasts/{podcastId}
```

Alternatively, the client will prompt for credentials when accessing a protected feed.


### Docker

PodBridge uses the [.NET SDK container building tools](https://learn.microsoft.com/dotnet/core/containers/overview) — there is no `Dockerfile`. Build and run a local image with:

```bash
dotnet publish src/PodBridge.Api/PodBridge.Api.csproj -t:PublishContainer -p:ContainerImageTag=local
docker run -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Production podbridge-api:local
```

## Endpoints

- `GET /api/podcasts/{podcastId}` — Returns the cached podcast feed for the specified show; RSS 2.0 + iTunes XML by default, or JSON with full episode details via `?format=json`
- `GET /api/podcasts` — Returns a JSON array with podcast metadata and public feed URLs
- `GET /healthz` — Health check endpoint (returns 200 OK if healthy)
- `GET /` and `GET /podcasts/{podcastId}` — Web UI: overview and per-podcast episode list

## Security

- `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, and a same-origin Content Security Policy are applied to all responses
- `/api/podcasts/{podcastId}` and `/api/podcasts` are protected by fixed-window rate limiting per remote IP; `/healthz` stays exempt for probe traffic
- The app trusts `X-Forwarded-For`/`X-Forwarded-Proto` from any immediate caller (needed for correct client IPs and rate limiting behind Hostim.dev, another managed container platform, or a self-hosted reverse proxy). Only deploy PodBridge so it is reachable exclusively through that trusted proxy, never directly from untrusted networks - otherwise a direct caller could spoof its IP via these headers
- For personal, non-commercial, private use only
- Before configuring any real data source, ensure you are entitled to do so under that source's terms of use and applicable law

## Development Notes

- Built with .NET 10 and ASP.NET Core, configuration bound via the [options pattern](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options) (`PodBridgeOptions`)
- Uses in-memory caching for podcast metadata; RSS/XML is rendered on demand in the API layer
- Background `EpisodeRefreshWorker` refreshes all configured podcasts on a configurable interval
- Full test coverage with NUnit + FluentAssertions, verified with [Stryker](https://stryker-mutator.io/) mutation testing
