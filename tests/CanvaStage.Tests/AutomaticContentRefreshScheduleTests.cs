using CanvaStage.Core.Services;
using Xunit;

namespace CanvaStage.Tests;

public sealed class AutomaticContentRefreshScheduleTests
{
    [Fact]
    public void NextCheckIsDueThirtySecondsAfterCompletionNotAnExtraTimerTickLater()
    {
        TestClock clock = new();
        AutomaticContentRefreshSchedule schedule = new(clock);
        Assert.True(schedule.IsDue);
        clock.Advance(TimeSpan.FromMilliseconds(200));
        schedule.Complete(true);
        clock.Advance(TimeSpan.FromSeconds(29.8));
        Assert.Equal(TimeSpan.FromMilliseconds(200), schedule.Remaining);
        clock.Advance(TimeSpan.FromMilliseconds(200));
        Assert.True(schedule.IsDue);
    }

    [Fact]
    public void FailureBackoffIsBoundedAndSuccessOrReenableResetsIt()
    {
        AutomaticContentRefreshSchedule schedule = new(new TestClock());
        foreach (int seconds in new[] { 60, 120, 240, 300, 300 })
        {
            schedule.Complete(false);
            Assert.Equal(TimeSpan.FromSeconds(seconds), schedule.Remaining);
        }
        schedule.Complete(true);
        Assert.Equal(TimeSpan.FromSeconds(30), schedule.Remaining);
        schedule.Complete(false);
        schedule.Reset();
        Assert.True(schedule.IsDue);
        schedule.Complete(false);
        Assert.Equal(TimeSpan.FromSeconds(60), schedule.Remaining);
    }

    [Fact]
    public void WallClockChangesDoNotDelayTheScheduledCheck()
    {
        TestClock clock = new();
        AutomaticContentRefreshSchedule schedule = new(clock);
        schedule.Complete(true);
        DateTimeOffset displayedDeadline = schedule.NextCheckAt;
        clock.UtcNow -= TimeSpan.FromHours(1);
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.True(schedule.IsDue);
        Assert.Equal(displayedDeadline, schedule.NextCheckAt);
    }

    private sealed class TestClock : TimeProvider
    {
        private long _timestamp;
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public override DateTimeOffset GetUtcNow() => UtcNow;
        public void Advance(TimeSpan elapsed)
        {
            _timestamp += elapsed.Ticks;
            UtcNow += elapsed;
        }
    }
}
