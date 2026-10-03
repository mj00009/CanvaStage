using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Services;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CanvaStage.Windows.Views;

public partial class OutputWindow : Window
{
    private readonly DispatcherTimer _clockTimer;
    private readonly OutputWindowViewModel _viewModel;
    private bool _isTextFitQueued;
    private bool _isRenderDiagnosticQueued;
    private bool _isClosed;
    private long _contentRevision;
    private Rectangle? _displayBounds;

    public OutputWindow(OutputWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        SizeChanged += OnOutputSizeChanged;
        Loaded += OnOutputLoaded;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        System.Windows.Media.RenderCapability.TierChanged += OnRenderingTierChanged;
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
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

    public void SetDisplayPlacement(Rectangle bounds)
    {
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
        double pixelsPerDip = System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (_viewModel.IsMonitor && !_viewModel.ShowFallbackImage
            && !_viewModel.PrimaryTextFits(PrimaryTextHost.ActualWidth, PrimaryTextHost.ActualHeight, pixelsPerDip))
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
        SizeChanged -= OnOutputSizeChanged;
        Loaded -= OnOutputLoaded;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
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

    private void OnOutputSizeChanged(object sender, SizeChangedEventArgs eventArgs) => ScheduleTextFit();

    private void OnOutputLoaded(object sender, RoutedEventArgs eventArgs)
    {
        ScheduleTextFit();
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
        if (eventArgs.PropertyName is nameof(OutputWindowViewModel.PrimaryText)
            or nameof(OutputWindowViewModel.EffectiveFontSize)
            or nameof(OutputWindowViewModel.ShowNextLine)
            or nameof(OutputWindowViewModel.NextLineFontSize)
            or nameof(OutputWindowViewModel.NextLineFontWeight))
        {
            ScheduleTextFit();
        }
    }

    private void ScheduleTextFit()
    {
        if (_isTextFitQueued)
        {
            return;
        }

        _isTextFitQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _isTextFitQueued = false;
            if (_isClosed) return;
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            _viewModel.FitPrimaryText(PrimaryTextHost.ActualWidth, PrimaryTextHost.ActualHeight, dpi.PixelsPerDip);
        }, DispatcherPriority.Render);
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
        SetWindowPos(handle, new nint(-1), bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0040);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out NativeRect bounds);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
