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
- Self-service "Add Podcast" flow: search by show title (or paste a ShowId directly), preview it, and add it without editing any configuration
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

Configure the GraphQL endpoint in `appsettings.json` under the `PodBridge` section, and the Postgres connection string under the standard `ConnectionStrings` section:

```json
{
  "ConnectionStrings": {
    "PodBridgeDb": "Host=localhost;Database=podbridge;Username=podbridge;Password=changeme"
  },
  "PodBridge": {
    "RefreshIntervalMinutes": 360,
    "RateLimitingPermitLimit": 15,
    "RateLimitingWindowMinutes": 5,
    "GraphQlEndpoint": "https://example.org/graphql",
    "Auth": {
      "Enabled": false,
      "UsernameHash": null,
      "PasswordHash": null
    }
  }
}
```

- `ConnectionStrings.PodBridgeDb`: Postgres connection string for the Podcasts list, used when the indirection below isn't configured (e.g. local development). Pending EF Core migrations are applied automatically at startup (retried with backoff); if the database stays unreachable, PodBridge still starts and serves already-cached feeds, but `/healthz` reports `Degraded` until the database becomes reachable again.
- `RefreshIntervalMinutes`: Background refresh interval for episodes and pre-generated feeds.
- `RateLimitingPermitLimit`: Maximum number of requests per remote IP and protected endpoint within the configured window. Default: `15`.
- `RateLimitingWindowMinutes`: Fixed-window length for rate limiting on `/api/podcasts/{podcastId}` and `/api/podcasts`. Default: `5`.
- `GraphQlEndpoint`: Absolute URI of the GraphQL endpoint used for show search, previews, and episode lookups.
- `Auth.Enabled`: Optional boolean flag to enable authentication. Default: `false`.
- `Auth.UsernameHash`: PBKDF2 hash of the Basic Auth username (required if `Auth.Enabled` is true). Generate it with `Scripts/New-CredentialHash.ps1` - the plaintext username is never stored.
- `Auth.PasswordHash`: PBKDF2 hash of the Basic Auth password (required if `Auth.Enabled` is true). Generate it with `Scripts/New-CredentialHash.ps1` - the plaintext password is never stored.

#### Database connection on managed platforms

Many managed container platforms auto-provision a Postgres instance and inject its credentials as several separate environment variables under platform-specific names (host/port/user/password/database), rather than a single connection string. To avoid hardcoding any platform's specific variable names in the app, PodBridge supports a level of indirection: configure which environment variable holds each part, and the app reads the actual value through that indirection.

```json
{
  "PodBridge": {
    "Database": {
      "HostEnvVar": "PGHOST",
      "PortEnvVar": "PGPORT",
      "UsernameEnvVar": "PGUSER",
      "PasswordEnvVar": "PGPASSWORD",
      "DatabaseNameEnvVar": "PGDATABASE"
    }
  }
}
```

- Each `*EnvVar` value is the *name* of another environment variable to read - substitute your platform's actual names here (this example uses libpq's conventional `PG*` names).
- If any of the five isn't configured, PodBridge falls back to `ConnectionStrings.PodBridgeDb` entirely.
- The app source code never references any specific hosting platform by name - only these five generic indirection keys.

#### Managing Podcasts

Podcasts are no longer configured via `appsettings.json` or environment variables - they live in the Postgres database and are managed entirely through the web UI:

- Open `/podcasts/add`, search by show title, and click "Add" on the desired result - or paste a ShowId directly if you already know it.
- The show's title/cover are previewed and the podcast is added immediately; the (potentially large) episode backfill then runs in the background, so the new podcast briefly shows as "not yet fetched" until the first refresh completes.
- A podcast's internal feed slug (`PodcastId`, used in `/api/podcasts/{podcastId}` and `/podcasts/{podcastId}`) is derived automatically from the show title.
- Adding the same show (same ShowId) twice is rejected.

#### Authentication

Authentication is opt-in via the `Auth.Enabled` configuration flag. When enabled, the web UI uses a cookie-backed login page and the API keeps HTTP Basic Authentication for podcatchers and other API clients.

The following endpoints stay public:

- `/healthz`
- `/openapi/v1.json`
- `/scalar`

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

- **External config file** (recommended for mounting the Auth hashes as a single file instead of per-key Docker secrets):
  - Mount a JSON file containing the full `PodBridge` section (Auth hashes, etc.) into the container, e.g. at `/data/podbridge.appsettings.json` via a persistent Volume.
  - Set the `PODBRIDGE_EXTERNAL_CONFIG_FILE_PATH` environment variable to that path; the feature is disabled entirely (no lookup at any default path) unless this variable is set.
  - Reloaded automatically when the file changes on disk, as long as its containing directory already exists at startup (`reloadOnChange` is otherwise disabled to avoid `FileSystemWatcher` falling back to a slow recursive watch of the nearest existing ancestor directory).

- **Local development**:
  - Use standard .NET configuration sources such as environment variables, `launchSettings.json`, or `dotnet user-secrets`.

**Client usage:**

Browser users sign in via the `/login` form before accessing the web UI.

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
- `GET /healthz` — Health check endpoint (returns 200 OK if healthy, Degraded if the Postgres database or the last episode refresh is unavailable)
- `GET /login` — Login page for the web UI
- `POST /logout` — Ends the current web UI session
- `GET /openapi/v1.json` — Public OpenAPI document for the protected API
- `GET /scalar` — Public Scalar reference UI for the protected API
- `GET /` and `GET /podcasts/{podcastId}` — Web UI: overview and per-podcast episode list
- `GET /podcasts/add` — Web UI: self-service "Add Podcast" flow (search by title or add by ShowId)

## Security

- `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, and a same-origin Content Security Policy are applied to all responses
- `/api/podcasts/{podcastId}` and `/api/podcasts` are protected by fixed-window rate limiting per remote IP; `/healthz` stays exempt for probe traffic
- The app trusts `X-Forwarded-For`/`X-Forwarded-Proto` from any immediate caller (needed for correct client IPs and rate limiting behind a managed container platform or a self-hosted reverse proxy). Only deploy PodBridge so it is reachable exclusively through that trusted proxy, never directly from untrusted networks - otherwise a direct caller could spoof its IP via these headers
- For personal, non-commercial, private use only
- Before configuring any real data source, ensure you are entitled to do so under that source's terms of use and applicable law

## Development Notes

- Built with .NET 10 and ASP.NET Core, configuration bound via the [options pattern](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options) (`PodBridgeOptions`)
- The Podcasts list lives in Postgres, accessed via EF Core (`AppDbContext`, `IDbContextFactory<AppDbContext>`, no repository abstraction); everything else (Auth, GraphQL endpoint, refresh/rate-limiting settings) stays in `PodBridgeOptions`/`IConfiguration`
- Uses in-memory caching for podcast metadata; RSS/XML is rendered on demand in the API layer
- Background `EpisodeRefreshWorker` refreshes all configured podcasts on a configurable interval, delegating the per-podcast fetch/cache logic to `IPodcastRefreshService` (shared with the "Add Podcast" flow's immediate post-add refresh)
- Full test coverage with NUnit + FluentAssertions, verified with [Stryker](https://stryker-mutator.io/) mutation testing
- System tests (`tests/Tests/System/SystemTests.cs`) spin up the app, WireMock, and a real Postgres instance via Testcontainers - they require a running Docker daemon; exclude them from a routine local run with `dotnet test --filter "TestCategory!=System"`
