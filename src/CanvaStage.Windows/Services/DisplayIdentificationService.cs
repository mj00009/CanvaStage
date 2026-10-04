using CanvaStage.Windows.Views;
using System.Windows.Threading;

namespace CanvaStage.Windows.Services;

public sealed class DisplayIdentificationService : IDisposable
{
    private readonly DisplayService _displayService;
    private readonly List<DisplayIdentificationWindow> _windows = [];
    private DispatcherTimer? _closeTimer;

    public DisplayIdentificationService(DisplayService displayService) => _displayService = displayService;

    internal IReadOnlyList<DisplayIdentificationWindow> WindowsForVerification => _windows;

    public void Show()
    {
        Close();
        IReadOnlyList<DisplayOption> displays = _displayService.GetDisplays();
        PrivacySafeLog.Write($"Identify displays requested: {displays.Count} available displays.");
        try
        {
            foreach (DisplayOption display in displays)
            {
                System.Windows.Forms.Screen? screen = _displayService.FindDisplay(display.ScreenDeviceName);
                if (screen is null)
                {
                    PrivacySafeLog.Write($"Display identification skipped: {display.ScreenDeviceName} is no longer available.");
                    continue;
                }
                DisplayIdentificationWindow window = new(new DisplayOptionViewModel(display.Number, display.FriendlyName,
                    $"{display.Width} × {display.Height} · {(display.IsPrimary ? "Primary" : "Extended")}"));
                _windows.Add(window);
                window.ShowOnDisplay(screen.Bounds);
            }
        }
        catch { Close(); throw; }

        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _closeTimer.Tick += OnCloseTimer;
        _closeTimer.Start();
    }

    private void OnCloseTimer(object? sender, EventArgs eventArgs) => Close();

    public void Dispose() => Close();

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
