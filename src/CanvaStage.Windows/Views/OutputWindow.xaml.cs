using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Services;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CanvaStage.Core.Models;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace CanvaStage.Windows.Views;

public partial class OutputWindow : Window
{
    private readonly DispatcherTimer _clockTimer;
    private readonly OutputWindowViewModel _viewModel;
    private bool _isRenderDiagnosticQueued;
    private bool _isClosed;
    private long _contentRevision;
    private Rectangle? _displayBounds;
    private Rectangle? _hiddenPreviewBounds;
    private OutputSnapshot? _renderedSnapshot;

    public OutputWindow(OutputWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnOutputLoaded;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.SnapshotChanging += OnSnapshotChanging;
        System.Windows.Media.RenderCapability.TierChanged += OnRenderingTierChanged;
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += OnClockTick;
        _clockTimer.Start();
    }

    public void SetPreviewPlacement()
    {
        _displayBounds = null;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        WindowState = WindowState.Normal;
        Width = 1000;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        PrivacySafeLog.Write("Output placement requested: resizable Preview.");
    }

    internal void PrepareHiddenPreview(Rectangle bounds)
    {
        if (IsVisible || _isClosed) return;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        nint handle = new WindowInteropHelper(this).EnsureHandle();
        // Move a hidden HWND to the intended monitor to obtain its actual DPI.
        // Do not show it, activate it, or change the live display's layout.
        if (_hiddenPreviewBounds != bounds)
        {
            if (SetWindowPos(handle, 0, bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0014))
                _hiddenPreviewBounds = bounds;
            else PrivacySafeLog.Write($"Offline Preview placement failed: Windows error {Marshal.GetLastWin32Error()}.");
        }
        double scale = Math.Max(96, GetDpiForWindow(handle)) / 96d;
        System.Windows.Size viewport = new(bounds.Width / scale, bounds.Height / scale);
        PreviewSurface.Width = viewport.Width;
        PreviewSurface.Height = viewport.Height;
        PreviewSurface.Measure(viewport);
        PreviewSurface.Arrange(new Rect(viewport));
        PreviewSurface.UpdateLayout();
    }

    public void SetDisplayPlacement(Rectangle bounds)
    {
        PreviewSurface.ClearValue(WidthProperty);
        PreviewSurface.ClearValue(HeightProperty);
        _hiddenPreviewBounds = null;
        _displayBounds = bounds;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        WindowState = WindowState.Normal;
        PrivacySafeLog.Write($"Output placement requested: display bounds {bounds}.");
    }

    public void ShowAndActivate()
    {
        if (!IsVisible)
        {
            Show();
        }

        Topmost = true;
        ApplyNativeDisplayPlacement();
        Activate();
        LogRenderingEnvironment("shown/activated");
    }

    internal Rectangle GetNativeBounds()
    {
        nint handle = new WindowInteropHelper(this).Handle;
        if (handle == 0 || !GetWindowRect(handle, out NativeRect bounds)) return Rectangle.Empty;
        return Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    }

    internal void VerifyTextFit()
    {
        UpdateLayout();
        if (_viewModel.IsMonitor && !_viewModel.ShowFallbackImage
            && !PrimaryTextHost.TextFits)
            throw new InvalidOperationException("Confidence output text is clipped in the verification scenario.");
        if (_viewModel.ShowNextLine && (NextLineText.TextWrapping != TextWrapping.NoWrap
            || NextLineText.TextTrimming != TextTrimming.WordEllipsis || NextLineText.ActualHeight > NextLineText.FontSize * 1.8))
            throw new InvalidOperationException("The next-line cue occupies more than one rendered line.");
        if (_viewModel.ShowNextLine && (PrimaryText.FontFamily.Source != NextLineText.FontFamily.Source
            || (!_viewModel.Snapshot.Appearance.IsNextLineBold && PrimaryText.FontWeight != NextLineText.FontWeight)))
            throw new InvalidOperationException("The next-line cue does not match the main text's font family/default weight.");
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
        _isClosed = true;
        System.Windows.Media.CompositionTarget.Rendering -= OnRenderDiagnostic;
        System.Windows.Media.RenderCapability.TierChanged -= OnRenderingTierChanged;
        Loaded -= OnOutputLoaded;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.SnapshotChanging -= OnSnapshotChanging;
        ClearTransition();
        _clockTimer.Stop();
        _clockTimer.Tick -= OnClockTick;
        base.OnClosed(eventArgs);
    }

    private void OnClockTick(object? sender, EventArgs eventArgs)
    {
        if (DataContext is OutputWindowViewModel viewModel)
        {
            viewModel.UpdateClock();
        }
    }

    internal bool IsTransitionActive => TransitionOverlay.Source is not null;

    private void OnSnapshotChanging(object? sender, OutputSnapshot next)
    {
        OutputSnapshot previous = _viewModel.Snapshot;
        bool pageChanged = previous.SelectedPageNumber != next.SelectedPageNumber;
        bool canFade = !_isClosed && IsVisible && next.Appearance.IsSlideFadeEnabled
            && previous.IsLive && next.IsLive && !previous.IsBlanked && !next.IsBlanked
            && ReferenceEquals(previous.Document, next.Document) && previous.Mode == next.Mode
            && previous.Layout == next.Layout && previous.Appearance == next.Appearance
            && _renderedSnapshot is { } rendered && rendered.IsLive && !rendered.IsBlanked
            && ReferenceEquals(rendered.Document, previous.Document) && rendered.Mode == previous.Mode
            && rendered.Layout == previous.Layout && rendered.Appearance == previous.Appearance;
        if (!canFade)
        {
            ClearTransition();
            return;
        }
        if (!pageChanged) return;

        // SnapshotChanging fires before bindings receive the new page. Capture the
        // current composite so rapid navigation replaces, rather than queues, fades.
        double width = TransitionSurface.ActualWidth;
        double height = TransitionSurface.ActualHeight;
        if (width <= 0 || height <= 0) return;
        DpiScale dpi = VisualTreeHelper.GetDpi(TransitionSurface);
        double scale = Math.Min(1, 4096 / Math.Max(width * dpi.DpiScaleX, height * dpi.DpiScaleY));
        try
        {
            RenderTargetBitmap frame = new(Math.Max(1, (int)Math.Ceiling(width * dpi.DpiScaleX * scale)),
                Math.Max(1, (int)Math.Ceiling(height * dpi.DpiScaleY * scale)),
                dpi.PixelsPerInchX * scale, dpi.PixelsPerInchY * scale, PixelFormats.Pbgra32);
            frame.Render(TransitionSurface);
            frame.Freeze();
            ClearTransition();
            TransitionOverlay.Source = frame;
            TransitionOverlay.Visibility = Visibility.Visible;
            DoubleAnimation fade = new(1, 0, TimeSpan.FromMilliseconds(next.Appearance.SlideFadeMilliseconds))
            {
                FillBehavior = FillBehavior.Stop
            };
            fade.Completed += (_, _) =>
            {
                if (ReferenceEquals(TransitionOverlay.Source, frame)) ClearTransition();
            };
            TransitionOverlay.BeginAnimation(OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or OutOfMemoryException)
        {
            // A failed visual capture must never prevent a live page change.
            ClearTransition();
            PrivacySafeLog.WriteException("Slide fade capture failed; showing the new page directly", exception);
        }
    }

    private void ClearTransition()
    {
        TransitionOverlay.BeginAnimation(OpacityProperty, null);
        TransitionOverlay.Source = null;
        TransitionOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnOutputLoaded(object sender, RoutedEventArgs eventArgs)
    {
        QueueRenderDiagnostic();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(OutputWindowViewModel.PrimaryText))
        {
            _contentRevision++;
            PrivacySafeLog.Write($"Output update queued: revision {_contentRevision}; page {_viewModel.Snapshot.SelectedPageNumber}; visible {IsVisible}; surface {(_displayBounds is null ? "Preview" : "Display")}.");
            QueueRenderDiagnostic();
        }
    }

    private void QueueRenderDiagnostic()
    {
        if (_isClosed || _isRenderDiagnosticQueued || !IsVisible || !_viewModel.Snapshot.IsLive) return;
        _isRenderDiagnosticQueued = true;
        System.Windows.Media.CompositionTarget.Rendering += OnRenderDiagnostic;
    }

    private void OnRenderDiagnostic(object? sender, EventArgs eventArgs)
    {
        // Observe one render-stage callback per coalesced content update. Do not force
        // layout/redraw or log text; diagnostics must not conceal a stale visual.
        System.Windows.Media.CompositionTarget.Rendering -= OnRenderDiagnostic;
        _isRenderDiagnosticQueued = false;
        if (_isClosed) return;
        _renderedSnapshot = _viewModel.Snapshot;
        PrivacySafeLog.Write(FormattableString.Invariant($"Output render stage: revision {_contentRevision}; page {_viewModel.Snapshot.SelectedPageNumber}; surface {(_displayBounds is null ? "Preview" : "Display")}; visible {IsVisible}; mode {_viewModel.Snapshot.Mode}; fallback {_viewModel.ShowFallbackImage}; text matches {PrimaryText.Text == _viewModel.PrimaryText}; characters {PrimaryText.Text.Length}/{_viewModel.PrimaryText.Length}; font {_viewModel.Snapshot.Appearance.FontFamily}; size {PrimaryText.FontSize:0.00}/{_viewModel.EffectiveFontSize:0.00}; host {PrimaryTextHost.ActualWidth:0.00}x{PrimaryTextHost.ActualHeight:0.00}; dpi {System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip:0.00}."));
    }

    private void OnRenderingTierChanged(object? sender, EventArgs eventArgs)
    {
        if (_isClosed || Dispatcher.HasShutdownStarted) return;
        if (Dispatcher.CheckAccess()) LogRenderingEnvironment("WPF rendering tier changed");
        else Dispatcher.BeginInvoke(() => { if (!_isClosed) LogRenderingEnvironment("WPF rendering tier changed"); });
    }

    private void LogRenderingEnvironment(string reason)
    {
        // Capability/placement telemetry is not a GPU-health check and makes no
        // changes to hardware acceleration, display configuration or render state.
        PrivacySafeLog.Write(FormattableString.Invariant($"Output graphics state ({reason}): WPF tier {System.Windows.Media.RenderCapability.Tier >> 16}; process render mode {System.Windows.Media.RenderOptions.ProcessRenderMode}; dpi {System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip:0.00}; native bounds {GetNativeBounds()}; target bounds {_displayBounds?.ToString() ?? "Preview"}; visible {IsVisible}."));
    }

    private void CloseOutput(object sender, RoutedEventArgs eventArgs) => Close();

    private void OutputWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == System.Windows.Input.Key.Escape)
        {
            Close();
        }
    }

    private void ApplyNativeDisplayPlacement()
    {
        if (_displayBounds is not Rectangle bounds) return;
        nint handle = new WindowInteropHelper(this).Handle;
        if (handle == 0) return;
        if (!SetWindowPos(handle, new nint(-1), bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0040))
            PrivacySafeLog.Write($"Native output placement failed: Windows error {Marshal.GetLastWin32Error()}; target {bounds}; actual {GetNativeBounds()}.");
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out NativeRect bounds);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
