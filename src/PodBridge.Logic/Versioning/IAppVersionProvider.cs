namespace PodBridge.Logic.Versioning;

public interface IAppVersionProvider
{
    /// <summary>
    /// Gets the application version without the source revision suffix (e.g. "1.4.2"), suitable for display in the UI.
    /// </summary>
    string DisplayVersion { get; }

    /// <summary>
    /// Gets the full informational version including the source revision suffix (e.g. "1.4.2+abcdef0"), suitable for
    /// diagnostics and telemetry (e.g. as the OpenTelemetry service version).
    /// </summary>
    string FullVersion { get; }
}
