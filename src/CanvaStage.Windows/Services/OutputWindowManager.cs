using CanvaStage.Core.Services;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using Microsoft.Win32;
using System.Windows;

namespace CanvaStage.Windows.Services;

public sealed class OutputWindowManager : IDisposable
{
    private readonly IOutputCoordinator _coordinator;
    private readonly DisplayService _displayService = new();
    private OutputWindow? _window;
    private Window? _previewWindow;
    private string? _previewDisplayId;
    private bool _previewLayoutQueued;
    private string? _targetDisplayId;
    private bool _disposed;

    public OutputWindowManager(IOutputCoordinator coordinator)
    {
        _coordinator = coordinator;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public event EventHandler? PreviewSourceChanged;
    public event EventHandler? DisplaysChanged;

    public FrameworkElement? PreviewSurface
    {
        get
        {
            if (_disposed) return null;
            EnsureWindow();
            return _window!.PreviewSurface;
        }
    }

    internal OutputWindow WindowForVerification { get { EnsureWindow(); return _window!; } }
    internal Window? PreviewWindowForVerification => _previewWindow;

    public void SetPreviewDisplay(string? displayId)
    {
        _previewDisplayId = displayId;
        EnsureWindow();
        QueuePreviewLayout();
    }

    public void ShowPreview(MainViewModel viewModel)
    {
        if (_previewWindow is null)
        {
            _previewWindow = new Window
            {
                Title = "CanvaStage — Confidence preview", Width = 1000, Height = 620,
                MinWidth = 400, MinHeight = 260, Background = System.Windows.Media.Brushes.Black,
                Icon = _window?.Icon, DataContext = viewModel, Owner = System.Windows.Application.Current?.MainWindow,
                Content = new ConfidencePreview(), WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            _previewWindow.Closed += (_, _) => _previewWindow = null;
            _previewWindow.PreviewKeyDown += (_, args) =>
            {
                if (args.Key == System.Windows.Input.Key.Escape) { _previewWindow?.Close(); args.Handled = true; }
            };
        }
        _previewWindow.Show();
        _previewWindow.Activate();
        QueuePreviewLayout();
    }

    public bool ShowOnDisplay(string? displayId)
    {
        PrivacySafeLog.Write($"Output start requested: Windows device {displayId ?? "none"}.");
        System.Windows.Forms.Screen? display = _displayService.FindDisplay(displayId);
        if (display is null)
        {
            PrivacySafeLog.Write("Output start rejected: requested display is unavailable or ambiguous; no fallback was used.");
            return false;
        }

        PrivacySafeLog.Write($"Output start resolved: Windows device {display.DeviceName}; primary {display.Primary}; bounds {display.Bounds}.");
        EnsureWindow();
        _targetDisplayId = display.DeviceName;
        _window!.SetDisplayPlacement(display.Bounds);
        _coordinator.StartOutput();
        _window.ShowAndActivate();
        OutputWindow shownWindow = _window;
        shownWindow.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () =>
        {
            if (_disposed || !ReferenceEquals(_window, shownWindow) || !shownWindow.IsVisible) return;
            // Observe the settled placement; do not force layout or conceal a bad move.
            System.Drawing.Rectangle actualBounds = shownWindow.GetNativeBounds();
            PrivacySafeLog.Write($"Output placement settled: requested device {display.DeviceName}; target {display.Bounds}; actual {actualBounds}; matches {actualBounds == display.Bounds}.");
        });
        return true;
    }

    public void StopOutput()
    {
        PrivacySafeLog.Write($"Output stop requested: live {_coordinator.Snapshot.IsLive}; visible {_window?.IsVisible == true}; target {_targetDisplayId ?? "none"}.");
        _targetDisplayId = null;
        _coordinator.StopOutput();
        // Destroy the live HWND rather than retaining a hidden fullscreen window.
        // Closed rebinds previews to a fresh, hidden renderer; Presenter updates
        // may continue, but cannot resurrect the stopped live window.
        _window?.Close();
        PrivacySafeLog.Write($"Output stop completed: live {_coordinator.Snapshot.IsLive}; visible {_window?.IsVisible == true}.");
        QueuePreviewLayout();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _previewWindow?.Close();
        _window?.Close();
    }

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        var viewModel = new OutputWindowViewModel(_coordinator);
        viewModel.PropertyChanged += OnOutputViewModelPropertyChanged;
        _window = new OutputWindow(viewModel);
        _window.Closed += OnWindowClosed;
        QueuePreviewLayout();
    }

    private void OnOutputViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        // PrimaryText is notified for every changed snapshot, including style/mode
        // changes. Clock ticks need the same hidden-only layout path.
        if (args.PropertyName is nameof(OutputWindowViewModel.PrimaryText) or nameof(OutputWindowViewModel.CurrentTime))
            QueuePreviewLayout();
    }

    private void QueuePreviewLayout()
    {
        if (_disposed || _previewLayoutQueued || _window is null || _window.IsVisible) return;
        _previewLayoutQueued = true;
        _window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            _previewLayoutQueued = false;
            if (_disposed || _window is null || _window.IsVisible) return;
            System.Windows.Forms.Screen? display = _displayService.FindDisplay(_previewDisplayId) ?? System.Windows.Forms.Screen.PrimaryScreen;
            if (display is not null) _window.PrepareHiddenPreview(display.Bounds);
        });
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs eventArgs)
    {
        System.Windows.Threading.Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (_disposed || dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        _ = dispatcher.InvokeAsync(() =>
        {
            bool stopped = RefreshDisplayConfiguration();
            if (!stopped) return;
            System.Windows.MessageBox.Show(
                "The selected confidence display is no longer available. Output was stopped safely. Choose a display or open Preview when you are ready.",
                "Confidence display disconnected",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        });
    }

    internal bool RefreshDisplayConfiguration()
    {
        if (_disposed) return false;
        string availableDisplays = string.Join("; ", System.Windows.Forms.Screen.AllScreens.Select(screen =>
            $"{screen.DeviceName} bounds {screen.Bounds} primary {screen.Primary}"));
        PrivacySafeLog.Write($"Windows display settings changed: live {_coordinator.Snapshot.IsLive}; target {_targetDisplayId ?? "Preview/none"}; available {availableDisplays}.");
        bool stopped = _coordinator.Snapshot.IsLive && _targetDisplayId is not null
            && _displayService.FindDisplay(_targetDisplayId) is null;
        if (stopped) StopOutput();
        DisplaysChanged?.Invoke(this, EventArgs.Empty);
        QueuePreviewLayout();
        return stopped;
    }

    private void OnWindowClosed(object? sender, EventArgs eventArgs)
    {
        if (_window is null)
        {
            return;
        }

        _window.Closed -= OnWindowClosed;
        PrivacySafeLog.Write("Output window closed.");
        if (_window.DataContext is IDisposable disposableViewModel)
        {
            if (_window.DataContext is OutputWindowViewModel viewModel)
                viewModel.PropertyChanged -= OnOutputViewModelPropertyChanged;
            disposableViewModel.Dispose();
        }
        _window = null;
        _targetDisplayId = null;
        _coordinator.StopOutput();
        if (!_disposed) PreviewSourceChanged?.Invoke(this, EventArgs.Empty);
    }
}
