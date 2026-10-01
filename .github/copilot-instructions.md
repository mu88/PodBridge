# PodBridge — Repo Context

## NSubstitute and Internal Types
- `InternalsVisibleTo("DynamicProxyGenAssembly2")` in `PodBridge.Logic.csproj` allows NSubstitute (Castle DynamicProxy) to mock `internal` classes directly (e.g. `GraphQlEpisodeSource`, `PodcastCache`). Nothing else references that assembly name explicitly — don't remove it as "unused".

## Test Host — Silent Service Removal
- `TestWebApplicationFactory.ConfigureWebHost` removes all registered `IHostedService` and `IEpisodeSource` implementations and substitutes a mock. Adding a new `IHostedService` in production code will silently **not** run in integration tests unless this factory is updated too.

## System Tests Require Docker
- `tests/Tests/System/SystemTests.cs` spins up the app plus a WireMock container via Testcontainers. These tests fail outright without a running Docker daemon — `dotnet test` alone does not fully cover them in Docker-less environments.

## `\n` vs `Environment.NewLine` Exception
- `RssFeedSerializer` intentionally uses `"\n"` instead of `Environment.NewLine` for deterministic RSS/XML output across Windows dev and Linux Docker (see inline comment at the call site). This is the one sanctioned exception to the general "always use `Environment.NewLine`" rule — do not "fix" it, and don't copy the `\n` pattern elsewhere without the same justification.
