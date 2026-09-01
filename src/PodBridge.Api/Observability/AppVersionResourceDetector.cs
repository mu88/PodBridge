using OpenTelemetry.Resources;
using PodBridge.Logic.Versioning;

namespace PodBridge.Api.Observability;

/// <summary>
/// Adds the app's <see cref="IAppVersionProvider.FullVersion"/> as the OpenTelemetry "service.version"
/// resource attribute. Implemented as an <see cref="IResourceDetector"/> (rather than passing the version
/// as a plain string to <c>ConfigureOpenTelemetry</c> during service registration) so that
/// <see cref="IAppVersionProvider"/> can be resolved from the real DI container: OpenTelemetry.Extensions.Hosting
/// only invokes detector factories registered via <see cref="ResourceBuilder.AddDetector(Func{IServiceProvider, IResourceDetector})"/>
/// when the TracerProvider/MeterProvider is actually built (after the host's ServiceProvider exists), unlike
/// <c>Program.cs</c>'s top-level statements, which run during service registration, before the container exists.
/// </summary>
internal sealed class AppVersionResourceDetector(IAppVersionProvider appVersionProvider) : IResourceDetector
{
    public Resource Detect() => new([new KeyValuePair<string, object>("service.version", appVersionProvider.FullVersion)]);
}
