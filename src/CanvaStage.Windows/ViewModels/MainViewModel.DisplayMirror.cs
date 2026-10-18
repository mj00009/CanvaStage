using CanvaStage.Windows.Services;
using CanvaStage.Windows.Views;
using System.Windows;

namespace CanvaStage.Windows.ViewModels;

public sealed partial class MainViewModel
{
    private DisplayMirrorService _displayMirror = null!;
    private Func<string, bool> _confirmLayoutChange = null!;
    private Func<bool> _keepMirroredLayout = null!;
    private DisplayOption? _mirrorAudienceDisplay;
    private bool _isDisplayLayoutBusy;
    private string _displayLayoutStatus = "Mirror the audience screen onto the confidence screen as a fallback. The primary operator screen stays separate.";

    public AsyncRelayCommand MirrorAudienceCommand { get; private set; } = null!;
    public AsyncRelayCommand RestoreExtendedLayoutCommand { get; private set; } = null!;
    public IReadOnlyList<DisplayOption> MirrorAudienceDisplays => Displays.Where(display => !display.IsPrimary
        && display.ScreenDeviceName != SelectedDisplay?.ScreenDeviceName).ToArray();
    public DisplayOption? MirrorAudienceDisplay
    {
        get => _mirrorAudienceDisplay;
        set { if (SetProperty(ref _mirrorAudienceDisplay, value)) NotifyDisplayLayoutState(); }
    }
    public bool IsDisplayLayoutBusy => _isDisplayLayoutBusy;
    public bool HasMirroringRestorePoint => _displayMirror?.HasRestorePoint == true;
    public string DisplayLayoutStatus { get => _displayLayoutStatus; private set => SetProperty(ref _displayLayoutStatus, value); }
    public string MirrorFallbackTitle => HasMirroringRestorePoint ? "Return to extended displays" : "Mirror the audience screen";
    public string MirrorFallbackTarget => HasMirroringRestorePoint
        ? "Restore the saved layout before selecting a separate confidence screen."
        : SelectedDisplay is { IsPrimary: false } target
        ? $"Confidence screen: {target.Label}" : "First select the Extended confidence screen in step 2 below.";

    private void ConfigureDisplayMirror(DisplayMirrorService? service, Func<string, bool>? confirm, Func<bool>? keep)
    {
        _displayMirror = service ?? new DisplayMirrorService();
        _confirmLayoutChange = confirm ?? ConfirmLayoutChange;
        _keepMirroredLayout = keep ?? (() => new KeepDisplayLayoutWindow().ShowDialog() == true);
        MirrorAudienceCommand = new AsyncRelayCommand(() => ChangeDisplayLayoutAsync(false), () => CanChangeDisplayLayout
            && !HasMirroringRestorePoint && SelectedDisplay is { IsPrimary: false } && MirrorAudienceDisplay is not null);
        RestoreExtendedLayoutCommand = new AsyncRelayCommand(() => ChangeDisplayLayoutAsync(true), () => CanChangeDisplayLayout && HasMirroringRestorePoint);
        if (HasMirroringRestorePoint) DisplayLayoutStatus = "A previous Extended layout is saved. Restore it before starting separate confidence output. Windows mirroring may still be active.";
    }
    private bool CanChangeDisplayLayout => !_isClosing && !_isDisplayLayoutBusy && !IsSimpleSetupRunning && !IsCanvaBusy;
    private void RefreshMirrorChoices()
    {
        if (_displayMirror is null) return;
        DisplayOption[] choices = MirrorAudienceDisplays.ToArray();
        _mirrorAudienceDisplay = choices.SingleOrDefault(display => display.ScreenDeviceName == _mirrorAudienceDisplay?.ScreenDeviceName)
            ?? (choices.Length == 1 ? choices[0] : null);
        OnPropertyChanged(nameof(MirrorAudienceDisplays));
        OnPropertyChanged(nameof(MirrorAudienceDisplay));
        NotifyDisplayLayoutState();
    }
    private void NotifyDisplayLayoutState()
    {
        OnPropertyChanged(nameof(IsDisplayLayoutBusy));
        OnPropertyChanged(nameof(HasMirroringRestorePoint));
        OnPropertyChanged(nameof(MirrorFallbackTitle));
        OnPropertyChanged(nameof(MirrorFallbackTarget));
        MirrorAudienceCommand?.RaiseCanExecuteChanged();
        RestoreExtendedLayoutCommand?.RaiseCanExecuteChanged();
        StartOutputCommand?.RaiseCanExecuteChanged();
        StartSimpleSetupCommand?.RaiseCanExecuteChanged();
    }
    internal async Task ChangeDisplayLayoutAsync(bool restore)
    {
        if (!CanChangeDisplayLayout) return;
        _isDisplayLayoutBusy = true;
        NotifyDisplayLayoutState();
        bool awaitingKeep = false;
        try
        {
            CancelDisplayReconnectOffer();
            DisplayOption? audience = MirrorAudienceDisplay;
            DisplayOption? confidence = SelectedDisplay;
            if (!restore && (audience is null || confidence is null || confidence.IsPrimary))
                throw new InvalidOperationException("Choose the audience screen here and the Extended confidence screen in step 2. The primary screen cannot be mirrored by this fallback.");
            long revision = _outputRequestRevision;
            DisplayLayoutChange change = await Task.Run(() => restore ? _displayMirror.PrepareRestore()
                : _displayMirror.PrepareMirror(audience!.ScreenDeviceName, confidence!.ScreenDeviceName));
            string message = restore
                ? "This will restore your previous Extended display layout. Separate confidence output will stay stopped until you start it again. Are you sure you want to do this?"
                : $"This will stop CanvaStage confidence output and switch {confidence!.Label} to mirror {audience!.Label}. Both screens will show the audience presentation. Your primary operator screen will stay separate. Are you sure you want to do this?";
            if (!_confirmLayoutChange(message)) { DisplayLayoutStatus = "Cancelled. CanvaStage did not change the display layout or stop output."; return; }
            if (_isClosing || revision != _outputRequestRevision || (!restore && (SelectedDisplay?.ScreenDeviceName != confidence!.ScreenDeviceName
                || MirrorAudienceDisplay?.ScreenDeviceName != audience!.ScreenDeviceName)))
                throw new InvalidOperationException("The output request or selected screen changed while confirmation was open. Try again when the setup is settled.");
            // Recheck before stopping output; Apply also rechecks immediately before the native change.
            await Task.Run(() => _displayMirror.RequireUnchanged(change.Before));
            StopOutput();
            if (restore)
            {
                await Task.Run(() => _displayMirror.ApplyRestore(change));
                DisplayLayoutStatus = "The previous Extended layout was restored. Check the confidence screen in step 2, then start output when ready.";
            }
            else
            {
                await Task.Run(() => _displayMirror.ApplyMirror(change));
                awaitingKeep = true;
                if (_isClosing || !_keepMirroredLayout())
                {
                    await Task.Run(_displayMirror.RevertMirror);
                    DisplayLayoutStatus = "Mirroring was not kept. The previous layout was restored; confidence output stays stopped.";
                }
                else DisplayLayoutStatus = "Audience mirroring is active. Show Canva's audience presentation on the audience screen; both screens display it. Use Restore extended layout to return to separate confidence output.";
                awaitingKeep = false;
            }
        }
        catch (Exception exception)
        {
            if (awaitingKeep && HasMirroringRestorePoint)
            {
                try { await Task.Run(_displayMirror.RevertMirror); }
                catch (Exception restoreException)
                {
                    PrivacySafeLog.WriteException("Automatic display layout restore did not finish", restoreException);
                    DisplayLayoutStatus = $"The display layout could not be restored: {restoreException.Message}";
                    return;
                }
            }
            PrivacySafeLog.WriteException("Display layout change did not finish", exception);
            DisplayLayoutStatus = $"Display layout change did not finish: {exception.Message}";
        }
        finally
        {
            _isDisplayLayoutBusy = false;
            if (!_isClosing) RefreshDisplays("Display layout action completed");
            NotifyDisplayLayoutState();
        }
    }
    private static bool ConfirmLayoutChange(string message)
    {
        Window? owner = System.Windows.Application.Current.MainWindow;
        return (owner is null
            ? System.Windows.MessageBox.Show(message, "Change Windows display layout?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            : System.Windows.MessageBox.Show(owner, message, "Change Windows display layout?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)) == MessageBoxResult.Yes;
    }
}
