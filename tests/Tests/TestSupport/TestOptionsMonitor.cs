using Microsoft.Extensions.Options;

namespace Tests.TestSupport;

/// <summary>
/// Minimal <see cref="IOptionsMonitor{TOptions}"/> stand-in for tests: exposes a fixed, settable
/// <see cref="CurrentValue"/> instead of reacting to real configuration reloads (which none of these
/// tests exercise - config-reload behavior itself is out of scope here, only "does the consumer read
/// options.CurrentValue instead of a stale options.Value" is).
/// </summary>
internal sealed class TestOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
{
    public T CurrentValue { get; } = currentValue;

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
