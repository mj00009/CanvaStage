using CanvaStage.Windows.ViewModels;
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
    private Rectangle? _displayBounds;

    public OutputWindow(OutputWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
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
