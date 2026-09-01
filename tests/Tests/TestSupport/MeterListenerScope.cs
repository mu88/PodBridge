using System.Diagnostics.Metrics;
using PodBridge.Logic;

namespace Tests.TestSupport;

/// <summary>
/// Records every measurement published to PodBridge's <see cref="Meter"/> (see <see cref="Observability"/>)
/// for the lifetime of a test, via a <see cref="MeterListener"/>. Counter.Add(...) calls are otherwise
/// invisible to tests - unlike Activity tags, they can't be asserted on by inspecting a returned object -
/// so mutations that remove or alter these calls (e.g. Statement mutations turning them into a no-op)
/// would otherwise survive undetected.
/// </summary>
internal sealed class MeterListenerScope : IDisposable
{
    private readonly MeterListener _listener;
    private readonly List<RecordedMeasurement> _measurements = [];

    public MeterListenerScope()
    {
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (string.Equals(instrument.Meter.Name, Observability.MeterName, StringComparison.Ordinal))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            _measurements.Add(new RecordedMeasurement(instrument.Name, measurement, tags.ToArray())));
        _listener.Start();
    }

    public IReadOnlyList<RecordedMeasurement> Measurements => _measurements;

    public void Dispose()
    {
        _listener.Dispose();
    }
}

internal sealed record RecordedMeasurement(string InstrumentName, long Value, KeyValuePair<string, object?>[] Tags)
{
    public object? GetTag(string key) => Tags.FirstOrDefault(tag => string.Equals(tag.Key, key, StringComparison.Ordinal)).Value;
}
