using CanvaStage.Windows.ViewModels;
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
    private Rectangle? _displayBounds;

    public OutputWindow(OutputWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        SizeChanged += OnOutputSizeChanged;
        Loaded += OnOutputLoaded;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
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
    }

    internal Rectangle GetNativeBounds()
    {
        nint handle = new WindowInteropHelper(this).Handle;
        if (handle == 0 || !GetWindowRect(handle, out NativeRect bounds)) return Rectangle.Empty;
        return Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    }

    internal void VerifyTextFit(double pixelsPerDip)
    {
        UpdateLayout();
        _viewModel.FitPrimaryText(PrimaryTextHost.ActualWidth, PrimaryTextHost.ActualHeight, pixelsPerDip);
        UpdateLayout();
        if (_viewModel.IsMonitor && !_viewModel.ShowFallbackImage
            && !_viewModel.PrimaryTextFits(PrimaryTextHost.ActualWidth, PrimaryTextHost.ActualHeight, pixelsPerDip))
            throw new InvalidOperationException("Confidence output text is clipped in the verification scenario.");
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
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

    private void OnOutputLoaded(object sender, RoutedEventArgs eventArgs) => ScheduleTextFit();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
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
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            _viewModel.FitPrimaryText(PrimaryTextHost.ActualWidth, PrimaryTextHost.ActualHeight, dpi.PixelsPerDip);
        }, DispatcherPriority.Render);
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
