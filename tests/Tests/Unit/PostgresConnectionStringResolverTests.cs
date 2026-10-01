using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using PodBridge.Persistence;

namespace Tests.Unit;

[TestFixture]
[Category("Unit")]
public class PostgresConnectionStringResolverTests
{
    [Test]
    public void Resolve_WithoutDatabaseIndirectionConfigured_FallsBackToConnectionStringsSection()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                { "ConnectionStrings:PodBridgeDb", "Host=localhost;Database=podbridge" },
            })
            .Build();

        // Act
        var result = PostgresConnectionStringResolver.Resolve(configuration);

        // Assert
        result.Should().Be("Host=localhost;Database=podbridge");
    }

    [Test]
    public void Resolve_WithFullIndirectionConfigured_BuildsConnectionStringFromReferencedEnvVars()
    {
        // Arrange: the *EnvVar keys hold the NAME of another config/env entry - simulating a hosting
        // platform that injects credentials as separate variables under its own, platform-specific names.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                { "PodBridge:Database:HostEnvVar", "SOME_PLATFORM_PGHOST" },
                { "PodBridge:Database:PortEnvVar", "SOME_PLATFORM_PGPORT" },
                { "PodBridge:Database:UsernameEnvVar", "SOME_PLATFORM_PGUSER" },
                { "PodBridge:Database:PasswordEnvVar", "SOME_PLATFORM_PGPASSWORD" },
                { "PodBridge:Database:DatabaseNameEnvVar", "SOME_PLATFORM_PGDATABASE" },
                { "SOME_PLATFORM_PGHOST", "db.internal" },
                { "SOME_PLATFORM_PGPORT", "6543" },
                { "SOME_PLATFORM_PGUSER", "podbridge-user" },
                { "SOME_PLATFORM_PGPASSWORD", "s3cret" },
                { "SOME_PLATFORM_PGDATABASE", "podbridge" },
            })
            .Build();

        // Act
        var result = PostgresConnectionStringResolver.Resolve(configuration);

        // Assert
        result.Should().Contain("Host=db.internal")
            .And.Contain("Port=6543")
            .And.Contain("Username=podbridge-user")
            .And.Contain("Password=s3cret")
            .And.Contain("Database=podbridge");
    }

    [Test]
    public void Resolve_WithPartialIndirectionConfigured_FallsBackToConnectionStringsSection()
    {
        // Arrange: only some of the five indirection keys are set - treated as "not configured" as a
        // whole, since a partial Postgres connection string would just fail to connect anyway.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                { "PodBridge:Database:HostEnvVar", "SOME_PLATFORM_PGHOST" },
                { "SOME_PLATFORM_PGHOST", "db.internal" },
                { "ConnectionStrings:PodBridgeDb", "Host=localhost;Database=podbridge" },
            })
            .Build();

        // Act
        var result = PostgresConnectionStringResolver.Resolve(configuration);

        // Assert
        result.Should().Be("Host=localhost;Database=podbridge");
    }

    [Test]
    public void Resolve_WithOnlyHostMissing_FallsBackToConnectionStringsSection()
    {
        // Arrange - isolates the "host is null" term of the OR chain: username/password/database are all
        // configured, only host is missing.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                { "PodBridge:Database:UsernameEnvVar", "SOME_PLATFORM_PGUSER" },
                { "PodBridge:Database:PasswordEnvVar", "SOME_PLATFORM_PGPASSWORD" },
                { "PodBridge:Database:DatabaseNameEnvVar", "SOME_PLATFORM_PGDATABASE" },
                { "SOME_PLATFORM_PGUSER", "podbridge-user" },
                { "SOME_PLATFORM_PGPASSWORD", "s3cret" },
                { "SOME_PLATFORM_PGDATABASE", "podbridge" },
                { "ConnectionStrings:PodBridgeDb", "Host=localhost;Database=podbridge" },
            })
            .Build();

        // Act
        var result = PostgresConnectionStringResolver.Resolve(configuration);

        // Assert
        result.Should().Be("Host=localhost;Database=podbridge");
    }

    [Test]
    public void Resolve_WithOnlyPasswordMissing_FallsBackToConnectionStringsSection()
    {
        // Arrange - isolates the "password is null" term of the OR chain: host/username/database are all
        // configured, only password is missing.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                { "PodBridge:Database:HostEnvVar", "SOME_PLATFORM_PGHOST" },
                { "PodBridge:Database:UsernameEnvVar", "SOME_PLATFORM_PGUSER" },
                { "PodBridge:Database:DatabaseNameEnvVar", "SOME_PLATFORM_PGDATABASE" },
                { "SOME_PLATFORM_PGHOST", "db.internal" },
                { "SOME_PLATFORM_PGUSER", "podbridge-user" },
                { "SOME_PLATFORM_PGDATABASE", "podbridge" },
                { "ConnectionStrings:PodBridgeDb", "Host=localhost;Database=podbridge" },
            })
            .Build();

        // Act
        var result = PostgresConnectionStringResolver.Resolve(configuration);

        // Assert
        result.Should().Be("Host=localhost;Database=podbridge");
    }

    [Test]
    public void Resolve_WithOnlyDatabaseMissing_FallsBackToConnectionStringsSection()
    {
        // Arrange - isolates the "database is null" term of the OR chain: host/username/password are all
        // configured, only database is missing.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                { "PodBridge:Database:HostEnvVar", "SOME_PLATFORM_PGHOST" },
                { "PodBridge:Database:UsernameEnvVar", "SOME_PLATFORM_PGUSER" },
                { "PodBridge:Database:PasswordEnvVar", "SOME_PLATFORM_PGPASSWORD" },
                { "SOME_PLATFORM_PGHOST", "db.internal" },
                { "SOME_PLATFORM_PGUSER", "podbridge-user" },
                { "SOME_PLATFORM_PGPASSWORD", "s3cret" },
                { "ConnectionStrings:PodBridgeDb", "Host=localhost;Database=podbridge" },
            })
            .Build();

        // Act
        var result = PostgresConnectionStringResolver.Resolve(configuration);

        // Assert
        result.Should().Be("Host=localhost;Database=podbridge");
    }

    [Test]
    public void Resolve_WithNoConfigurationAtAll_ReturnsNull()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var result = PostgresConnectionStringResolver.Resolve(configuration);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void Resolve_WithNonNumericPort_FallsBackToDefaultPostgresPortWithoutThrowing()
    {
        // Arrange - port is present but fails int.TryParse; the builder should just keep its own default
        // port rather than the resolver throwing or propagating a parse failure.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                { "PodBridge:Database:HostEnvVar", "SOME_PLATFORM_PGHOST" },
                { "PodBridge:Database:PortEnvVar", "SOME_PLATFORM_PGPORT" },
                { "PodBridge:Database:UsernameEnvVar", "SOME_PLATFORM_PGUSER" },
                { "PodBridge:Database:PasswordEnvVar", "SOME_PLATFORM_PGPASSWORD" },
                { "PodBridge:Database:DatabaseNameEnvVar", "SOME_PLATFORM_PGDATABASE" },
                { "SOME_PLATFORM_PGHOST", "db.internal" },
                { "SOME_PLATFORM_PGPORT", "not-a-number" },
                { "SOME_PLATFORM_PGUSER", "podbridge-user" },
                { "SOME_PLATFORM_PGPASSWORD", "s3cret" },
                { "SOME_PLATFORM_PGDATABASE", "podbridge" },
            })
            .Build();

        // Act
        var result = PostgresConnectionStringResolver.Resolve(configuration);

        // Assert - port parsing is skipped entirely (no exception), so the builder falls back to its own
        // default port rather than one derived from the invalid value.
        result.Should().Contain("Host=db.internal").And.NotContain("Port=");
    }

    [Test]
    public void Resolve_WithPortEnvVarConfiguredButPointingToAnUnsetVariable_FallsBackToDefaultPostgresPort()
    {
        // Arrange - isolates the "port is not null" operand of the guard from the "PortEnvVar itself
        // configured" case: PortEnvVar names a variable, but that variable is never actually set,
        // so ReadIndirectValue's configuration[envVarName] lookup yields null (distinct from PortEnvVar
        // not being configured at all, which short-circuits earlier in ReadIndirectValue).
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                { "PodBridge:Database:HostEnvVar", "SOME_PLATFORM_PGHOST" },
                { "PodBridge:Database:PortEnvVar", "SOME_PLATFORM_PGPORT" },
                { "PodBridge:Database:UsernameEnvVar", "SOME_PLATFORM_PGUSER" },
                { "PodBridge:Database:PasswordEnvVar", "SOME_PLATFORM_PGPASSWORD" },
                { "PodBridge:Database:DatabaseNameEnvVar", "SOME_PLATFORM_PGDATABASE" },
                { "SOME_PLATFORM_PGHOST", "db.internal" },
                { "SOME_PLATFORM_PGUSER", "podbridge-user" },
                { "SOME_PLATFORM_PGPASSWORD", "s3cret" },
                { "SOME_PLATFORM_PGDATABASE", "podbridge" },
            })
            .Build();

        // Act
        var result = PostgresConnectionStringResolver.Resolve(configuration);

        // Assert
        result.Should().Contain("Host=db.internal").And.NotContain("Port=");
    }
}
