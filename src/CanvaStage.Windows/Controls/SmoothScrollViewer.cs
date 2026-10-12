using CanvaStage.Windows.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;

namespace CanvaStage.Windows.Controls;

/// <summary>Preserves high-resolution wheel deltas instead of turning every delta into full lines.</summary>
public sealed class SmoothScrollViewer : System.Windows.Controls.ScrollViewer
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    private double _target;
    private long _lastTick;
    private double _animatedOffset = double.NaN;

    public SmoothScrollViewer()
    {
        CanContentScroll = false;
        _timer.Tick += OnTick;
        Unloaded += (_, _) => _timer.Stop();
        ScrollChanged += (_, args) =>
        {
            if (args.ExtentHeightChange == 0 && args.ViewportHeightChange == 0
                && Math.Abs(VerticalOffset - _animatedOffset) < 1) return;
            // Scrollbar drag, keyboard, BringIntoView and programmatic scroll
            // win immediately; never animate back to an obsolete wheel target.
            if (args.VerticalChange != 0 || args.ExtentHeightChange != 0 || args.ViewportHeightChange != 0)
                _timer.Stop();
        };
    }

    protected override void OnMouseWheel(MouseWheelEventArgs args)
    {
        if (args.Handled) return;
        double origin = _timer.IsEnabled ? _target : VerticalOffset;
        double target = WheelScrollPolicy.Target(origin, args.Delta, ScrollableHeight,
            SystemParameters.WheelScrollLines, ViewportHeight);
        if (Math.Abs(target - VerticalOffset) < 0.1 && !_timer.IsEnabled) return;
        _target = target;
        _lastTick = Stopwatch.GetTimestamp();
        _timer.Start();
        args.Handled = true;
    }

    internal void ScrollWheelForVerification(int delta)
        => OnMouseWheel(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta));

    private void OnTick(object? sender, EventArgs args)
    {
        double elapsed = Math.Clamp(Stopwatch.GetElapsedTime(_lastTick).TotalSeconds, 0, 0.1);
        _lastTick = Stopwatch.GetTimestamp();
        _target = Math.Clamp(_target, 0, ScrollableHeight);
        double difference = _target - VerticalOffset;
        double next = Math.Abs(difference) < 0.5 ? _target
            : VerticalOffset + difference * (1 - Math.Exp(-elapsed / 0.045));
        _animatedOffset = next;
        ScrollToVerticalOffset(next);
        if (Math.Abs(_target - next) < 0.1) _timer.Stop();
    }
}
