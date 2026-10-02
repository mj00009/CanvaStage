namespace CanvaStage.Core.Services;

/// <summary>One deadline per completed check, using a monotonic clock for elapsed time.</summary>
public sealed class AutomaticContentRefreshSchedule
{
    private readonly TimeProvider _clock;
    private readonly TimeSpan _interval;
    private long _scheduledAt;
    private TimeSpan _delay;
    private int _failures;

    public AutomaticContentRefreshSchedule(TimeProvider? clock = null, TimeSpan? interval = null)
    {
        _clock = clock ?? TimeProvider.System;
        _interval = interval ?? TimeSpan.FromSeconds(30);
        if (_interval <= TimeSpan.Zero || _interval > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(interval));
        Reset();
    }

    public DateTimeOffset NextCheckAt { get; private set; }
    public TimeSpan Remaining => TimeSpan.FromTicks(Math.Max(0,
        (_delay - _clock.GetElapsedTime(_scheduledAt)).Ticks));
    public bool IsDue => Remaining == TimeSpan.Zero;

    public void Reset()
    {
        _failures = 0;
        Schedule(TimeSpan.Zero);
    }

    public void Complete(bool succeeded)
    {
        _failures = succeeded ? 0 : Math.Min(4, _failures + 1);
        Schedule(TimeSpan.FromSeconds(Math.Min(300, _interval.TotalSeconds * Math.Pow(2, _failures))));
    }

    private void Schedule(TimeSpan delay)
    {
        _scheduledAt = _clock.GetTimestamp();
        _delay = delay;
        NextCheckAt = _clock.GetUtcNow() + delay;
    }
}
