using CanvaStage.Core.Models;
using CanvaStage.Windows.Services;
using System.Windows;
using System.Windows.Threading;

namespace CanvaStage.Windows.ViewModels;

public sealed partial class MainViewModel
{
    private readonly DisplayReconnectPolicy _displayReconnectPolicy = new();
    private readonly DispatcherTimer _displayReconnectTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Func<DisplayOption, bool> _confirmDisplayReconnect;
    private DisplayOption? _pendingReconnectDisplay;
    private bool _isDisplayReconnectPromptOpen;

    private void ObserveDisplayReconnect(IReadOnlyList<DisplayOption> displays)
    {
        DisplayOption? target = _displayReconnectPolicy.Observe(displays, _preferredDisplayId);
        if (target is null) return;
        _pendingReconnectDisplay = target;
        PrivacySafeLog.Write($"Remembered Extended display returned: device {target.ScreenDeviceName}; output live {IsLive}. Awaiting a settled display check and operator confirmation.");
        _displayReconnectTimer.Start();
    }

    private void CancelDisplayReconnectOffer()
    {
        _pendingReconnectDisplay = null;
        _displayReconnectTimer.Stop();
    }

    private bool CanOfferDisplayReconnect => !_isClosing && !IsLive && !_simpleSetupOutputCancelled && !IsDisplayLayoutBusy && !HasMirroringRestorePoint
        && Snapshot.Document.Id != "demo-sunday" && Snapshot.Document.Id == _defaultPresentationId
        && Snapshot.Document.Pages.Count > 0;

    private void OnDisplayReconnectTick(object? sender, EventArgs args)
    {
        try { OfferDisplayReconnect(); }
        catch (Exception exception)
        {
            CancelDisplayReconnectOffer();
            PrivacySafeLog.WriteException("Remembered display reconnect offer could not be completed", exception);
        }
    }

    internal void OfferDisplayReconnect()
    {
        if (_isDisplayReconnectPromptOpen || _pendingReconnectDisplay is null) return;
        if (!CanOfferDisplayReconnect) { CancelDisplayReconnectOffer(); return; }
        // A download/setup may still be finishing. Keep one pending offer, never
        // start another download or interfere with its output-start decision.
        if (IsCanvaBusy || IsSimpleSetupRunning || _outputWindowManager.IsDisplayWarningOpen) return;
        DisplayOption? target = DisplayReconnectPolicy.ResolveTarget(_getDisplays(), _preferredDisplayId);
        if (target is null || target.Id != _pendingReconnectDisplay.Id)
        {
            CancelDisplayReconnectOffer();
            return;
        }
        if (target.ScreenDeviceName != _pendingReconnectDisplay.ScreenDeviceName)
        {
            // Windows may renumber a receiver while establishing a cast session.
            _pendingReconnectDisplay = target;
            return;
        }

        CancelDisplayReconnectOffer();
        long revision = _outputRequestRevision;
        PresentationDocument document = Snapshot.Document;
        bool accepted;
        _isDisplayReconnectPromptOpen = true;
        try { accepted = _confirmDisplayReconnect(target); }
        finally { _isDisplayReconnectPromptOpen = false; }
        PrivacySafeLog.Write($"Remembered display reconnect response: accepted {accepted}; device {target.ScreenDeviceName}.");
        if (!accepted) return;

        // Native dialogs run a nested dispatcher loop. Recheck after the answer:
        // Stop, shutdown, a new deck, or another disconnect invalidates consent.
        DisplayOption? current = DisplayReconnectPolicy.ResolveTarget(_getDisplays(), _preferredDisplayId);
        if (!CanOfferDisplayReconnect || IsCanvaBusy || IsSimpleSetupRunning
            || revision != _outputRequestRevision || !ReferenceEquals(document, Snapshot.Document)
            || current?.Id != target.Id || current.ScreenDeviceName != target.ScreenDeviceName)
        {
            PrivacySafeLog.Write("Remembered display reconnect start suppressed: output intent, content, or target changed while confirmation was open.");
            return;
        }
        SelectedDisplay = current;
        _outputRequestRevision++;
        if (_outputWindowManager.ShowOnDisplay(current.ScreenDeviceName))
            SimpleSetupStatus = "Confidence output restarted on the remembered display using the cached presentation. No new download was requested.";
        else
            SimpleSetupStatus = "The display disconnected before output could start. Reconnect it in Windows using Extend, then choose Start output in Live control.";
    }

    private static bool ConfirmDisplayReconnect(DisplayOption target)
    {
        string message = $"Do you want CanvaStage to start output on {target.Label}?\n\nThe current cached presentation will be used without repeating Setup or downloading it again. Choose No to keep output off.";
        Window? owner = System.Windows.Application.Current?.MainWindow;
        return (owner is null
            ? System.Windows.MessageBox.Show(message, "Confidence display available", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            : System.Windows.MessageBox.Show(owner, message, "Confidence display available", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)) == MessageBoxResult.Yes;
    }
}
