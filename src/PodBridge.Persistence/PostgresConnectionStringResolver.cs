using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PodBridge.Persistence;

/// <summary>
/// Resolves the Postgres connection string without hardcoding any hosting-platform-specific environment
/// variable names in code. Instead of reading e.g. "PGHOST" directly, this reads a small set of
/// *indirection* config keys (PodBridge:Database:HostEnvVar etc.) whose VALUE is the actual environment
/// variable name to look up - that mapping is supplied entirely through configuration (env vars/appsettings),
/// so the app itself stays agnostic of any specific hosting platform's naming scheme. If the indirection
/// isn't configured (e.g. local development), falls back to the conventional single
/// "ConnectionStrings:PodBridgeDb" connection string.
/// </summary>
public static class PostgresConnectionStringResolver
{
    private const string DatabaseSectionName = "PodBridge:Database";

    public static string? Resolve(IConfiguration configuration)
    {
        var databaseSection = configuration.GetSection(DatabaseSectionName);

        var host = ReadIndirectValue(configuration, databaseSection["HostEnvVar"]);
        var port = ReadIndirectValue(configuration, databaseSection["PortEnvVar"]);
        var username = ReadIndirectValue(configuration, databaseSection["UsernameEnvVar"]);
        var password = ReadIndirectValue(configuration, databaseSection["PasswordEnvVar"]);
        var database = ReadIndirectValue(configuration, databaseSection["DatabaseNameEnvVar"]);

        if (host is null || username is null || password is null || database is null)
        {
            return configuration.GetConnectionString("PodBridgeDb");
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Username = username,
            Password = password,
            Database = database,
        };

        if (port is not null && int.TryParse(port, System.Globalization.CultureInfo.InvariantCulture, out var parsedPort))
        {
            connectionStringBuilder.Port = parsedPort;
        }

        return connectionStringBuilder.ConnectionString;
    }

    // "envVarName" here is itself the resolved VALUE of e.g. PodBridge:Database:HostEnvVar (a name like
    // "PGHOST"), not a hardcoded key - reading it through IConfiguration (not Environment.GetEnvironmentVariable
    // directly) keeps this testable via a plain in-memory configuration source, since ASP.NET Core's default
    // host builder already surfaces all OS environment variables as top-level IConfiguration keys.
    private static string? ReadIndirectValue(IConfiguration configuration, string? envVarName)
    {
        return string.IsNullOrWhiteSpace(envVarName) ? null : configuration[envVarName];
    }
}
