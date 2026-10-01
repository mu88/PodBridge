using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PodBridge.Logic.Versioning;

/// <summary>
/// Reads the running application's version from its entry assembly's <see cref="AssemblyInformationalVersionAttribute" />.
/// </summary>
internal sealed class AppVersionProvider : IAppVersionProvider
{
    private const string UnknownVersion = "unknown";

    public AppVersionProvider()
        : this(FindInformationalVersionFromEntryAssembly())
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

    // Reading the real entry assembly's attributes can't be meaningfully varied from a unit test (there's
    // only ever one actual entry assembly per test run, so the "no attribute"/"no entry assembly" branches
    // are practically untestable here) - the branch-relevant logic itself is fully covered via the
    // string-accepting constructor above (see AppVersionProviderTests).
    [ExcludeFromCodeCoverage]
    private static string? FindInformationalVersionFromEntryAssembly() =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
}
