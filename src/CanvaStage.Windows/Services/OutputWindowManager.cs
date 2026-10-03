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
    private string? _targetDisplayId;
    private bool _disposed;

    public OutputWindowManager(IOutputCoordinator coordinator)
    {
        _coordinator = coordinator;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void ShowPreview()
    {
        EnsureWindow();
        _targetDisplayId = null;
        _window!.SetPreviewPlacement();
        _coordinator.StartOutput();
        _window.ShowAndActivate();
    }

    public bool ShowOnDisplay(string? displayId)
    {
        System.Windows.Forms.Screen? display = _displayService.FindDisplay(displayId);
        if (display is null)
        {
            return false;
        }

        EnsureWindow();
        _targetDisplayId = display.DeviceName;
        _window!.SetDisplayPlacement(display.Bounds);
        _coordinator.StartOutput();
        _window.ShowAndActivate();
        return true;
    }

    public void StopOutput()
    {
        PrivacySafeLog.Write("Output stopped; window will be hidden.");
        _targetDisplayId = null;
        _coordinator.StopOutput();
        if (_window is not null)
        {
            _window.Hide();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _window?.Close();
    }

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        var viewModel = new OutputWindowViewModel(_coordinator);
        _window = new OutputWindow(viewModel);
        _window.Closed += OnWindowClosed;
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
            if (_disposed) return;
            string availableDisplays = string.Join("; ", System.Windows.Forms.Screen.AllScreens.Select(screen =>
                $"{screen.DeviceName} bounds {screen.Bounds} primary {screen.Primary}"));
            PrivacySafeLog.Write($"Windows display settings changed: live {_coordinator.Snapshot.IsLive}; target {_targetDisplayId ?? "Preview/none"}; available {availableDisplays}.");
            if (_disposed || !_coordinator.Snapshot.IsLive || _targetDisplayId is null
                || _displayService.FindDisplay(_targetDisplayId) is not null)
            {
                return;
            }

            StopOutput();
            System.Windows.MessageBox.Show(
                "The selected confidence display is no longer available. Output was stopped safely. Choose a display or open Preview when you are ready.",
                "Confidence display disconnected",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        });
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
            disposableViewModel.Dispose();
        }
        _window = null;
        _targetDisplayId = null;
        _coordinator.StopOutput();
    }
}
