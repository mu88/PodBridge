using Microsoft.Extensions.Options;

namespace Tests.TestSupport;

/// <summary>
/// Minimal <see cref="IOptionsSnapshot{TOptions}"/> stand-in for tests: exposes a fixed <see cref="Value"/>
/// instead of a real per-scope rebind against reloaded configuration, which none of these tests exercise -
/// only "does the consumer resolve IOptionsSnapshot instead of IOptions" is in scope here.
/// </summary>
internal sealed class TestOptionsSnapshot<T>(T value) : IOptionsSnapshot<T>
    where T : class
{
    public T Value { get; } = value;

    public T Get(string? name) => Value;
}
