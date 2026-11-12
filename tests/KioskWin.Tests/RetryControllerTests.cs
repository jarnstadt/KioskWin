using KioskWin.Core;
using Xunit;

namespace KioskWin.Tests;

public class RetryControllerTests
{
    private sealed class FakeScheduler : IRetryScheduler
    {
        public bool IsRunning { get; private set; }
        public TimeSpan LastInterval { get; private set; }
        private Action? _onTick;

        public void Start(TimeSpan interval, Action onTick)
        {
            if (IsRunning) return;
            IsRunning = true;
            LastInterval = interval;
            _onTick = onTick;
        }

        public void Stop()
        {
            IsRunning = false;
            _onTick = null;
        }

        public void FireTick() => _onTick?.Invoke();
    }

    [Fact]
    public void ReportFailure_starts_retrying()
    {
        var sched = new FakeScheduler();
        var ctrl = new RetryController(TimeSpan.FromSeconds(15), sched);

        ctrl.ReportFailure();

        Assert.Equal(RetryState.Retrying, ctrl.State);
        Assert.True(sched.IsRunning);
        Assert.Equal(TimeSpan.FromSeconds(15), sched.LastInterval);
    }

    [Fact]
    public void Tick_raises_ShouldRetry()
    {
        var sched = new FakeScheduler();
        var ctrl = new RetryController(TimeSpan.FromSeconds(1), sched);
        var fired = 0;
        ctrl.ShouldRetry += () => fired++;

        ctrl.ReportFailure();
        sched.FireTick();
        sched.FireTick();

        Assert.Equal(2, fired);
    }

    [Fact]
    public void ReportSuccess_stops_and_returns_to_idle()
    {
        var sched = new FakeScheduler();
        var ctrl = new RetryController(TimeSpan.FromSeconds(1), sched);

        ctrl.ReportFailure();
        ctrl.ReportSuccess();

        Assert.Equal(RetryState.Idle, ctrl.State);
        Assert.False(sched.IsRunning);
    }

    [Fact]
    public void Double_failure_does_not_restart_or_change_interval()
    {
        var sched = new FakeScheduler();
        var ctrl = new RetryController(TimeSpan.FromSeconds(7), sched);

        ctrl.ReportFailure();
        ctrl.ReportFailure(); // A subsequent failure should be ignored.

        Assert.Equal(RetryState.Retrying, ctrl.State);
        Assert.Equal(TimeSpan.FromSeconds(7), sched.LastInterval);
    }
}
