using System.Diagnostics;

namespace Tests.TestSupport;

/// <summary>
/// Registers an <see cref="ActivityListener"/> for the lifetime of a test so that
/// <see cref="ActivitySource.StartActivity()"/> calls in production code actually create
/// (non-null) activities, exercising the "activity is present" branches that would
/// otherwise be skipped (an <see cref="ActivitySource"/> without any listener always
/// returns <see langword="null"/> from <c>StartActivity</c>).
/// </summary>
internal sealed class ActivityListenerScope : IDisposable
{
    private readonly ActivityListener _listener;

    public ActivityListenerScope()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
    }
}
