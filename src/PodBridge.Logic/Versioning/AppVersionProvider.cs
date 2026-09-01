using System.Reflection;

namespace PodBridge.Logic.Versioning;

/// <summary>
/// Reads the running application's version from its entry assembly's <see cref="AssemblyInformationalVersionAttribute" />.
/// </summary>
internal sealed class AppVersionProvider : IAppVersionProvider
{
    private const string UnknownVersion = "unknown";

    public AppVersionProvider()
        : this(Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion)
    {
    }

    internal AppVersionProvider(string? informationalVersion)
    {
        FullVersion = string.IsNullOrWhiteSpace(informationalVersion) ? UnknownVersion : informationalVersion;

        // The .NET SDK appends a "+" plus the git SHA to the informational version by default; that suffix is
        // valuable for telemetry and diagnostics purposes, exposed via FullVersion, but too noisy for a UI footer.
        var sourceRevisionSeparatorIndex = FullVersion.IndexOf('+', StringComparison.Ordinal);
        DisplayVersion = sourceRevisionSeparatorIndex >= 0 ? FullVersion[..sourceRevisionSeparatorIndex] : FullVersion;
    }

    public string DisplayVersion { get; }

    public string FullVersion { get; }
}
