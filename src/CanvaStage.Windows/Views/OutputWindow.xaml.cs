using CanvaStage.Windows.ViewModels;
using System.Drawing;
using System.Windows;
using System.Windows.Threading;

namespace CanvaStage.Windows.Views;

public partial class OutputWindow : Window
{
    private readonly DispatcherTimer _clockTimer;

    public OutputWindow(OutputWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _clockTimer.Tick += OnClockTick;
        _clockTimer.Start();
    }

    public void SetPreviewPlacement()
    {
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        WindowState = WindowState.Normal;
        Width = 1000;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    public void SetDisplayPlacement(Rectangle bounds)
    {
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
        Activate();
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
}
