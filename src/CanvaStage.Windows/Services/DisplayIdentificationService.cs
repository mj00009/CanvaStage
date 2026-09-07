using CanvaStage.Windows.Views;
using System.Windows.Threading;

namespace CanvaStage.Windows.Services;

public sealed class DisplayIdentificationService
{
    private readonly DisplayService _displayService;
    private readonly List<DisplayIdentificationWindow> _windows = [];
    private DispatcherTimer? _closeTimer;

    public DisplayIdentificationService(DisplayService displayService) => _displayService = displayService;

    public void Show()
    {
        Close();
        foreach (DisplayOption display in _displayService.GetDisplays())
        {
            System.Windows.Forms.Screen? screen = _displayService.FindDisplay(display.Id);
            if (screen is null) continue;
            var window = new DisplayIdentificationWindow(new DisplayOptionViewModel(display.Number, display.FriendlyName,
                $"{display.Width} × {display.Height} · {(display.IsPrimary ? "Primary" : "Extended")}"))
            {
                WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                Left = screen.Bounds.Left + Math.Max(20, (screen.Bounds.Width - 360) / 2d),
                Top = screen.Bounds.Top + Math.Max(20, (screen.Bounds.Height - 280) / 2d),
                Width = Math.Min(360, screen.Bounds.Width - 40),
                Height = Math.Min(280, screen.Bounds.Height - 40)
            };
            _windows.Add(window);
            window.Show();
        }

        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _closeTimer.Tick += OnCloseTimer;
        _closeTimer.Start();
    }

    private void OnCloseTimer(object? sender, EventArgs eventArgs) => Close();

    private void Close()
    {
        if (_closeTimer is not null)
        {
            _closeTimer.Stop();
            _closeTimer.Tick -= OnCloseTimer;
            _closeTimer = null;
        }
        foreach (DisplayIdentificationWindow window in _windows.ToArray()) window.Close();
        _windows.Clear();
    }
}
