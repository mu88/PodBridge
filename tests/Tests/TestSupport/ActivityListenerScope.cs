using System.Diagnostics;

namespace Tests.TestSupport;

/// <summary>
/// Registers an <see cref="ActivityListener"/> for the lifetime of a test so that
/// <see cref="ActivitySource.StartActivity()"/> calls in production code actually create
/// (non-null) activities, exercising the "activity is present" branches that would
/// otherwise be skipped (an <see cref="ActivitySource"/> without any listener always
/// returns <see langword="null"/> from <c>StartActivity</c>). Also captures every stopped
/// activity so tests can assert on the tag values recorded by production code, not just
/// on the "activity is present" branch being taken.
/// </summary>
internal sealed class ActivityListenerScope : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _stoppedActivities = [];

    public ActivityListenerScope()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = _stoppedActivities.Add,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public IReadOnlyList<Activity> StoppedActivities => _stoppedActivities;

    public void Dispose()
    {
        _listener.Dispose();
    }
}
