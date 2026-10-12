using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Windows.Services;

namespace CanvaStage.Windows.ViewModels;

public sealed partial class MainViewModel
{
    private string? _defaultPresentationId;
    private string? _defaultPresentationTitle;
    private bool _isAdvancedSetup;
    private bool _isSimpleSetupRunning;
    private double _simpleSetupProgress;
    private string _simpleSetupStatus = "Start setup connects Canva, checks the last-used deck for changes, and starts output on the saved Extended display.";
    private long _outputRequestRevision;
    private bool _simpleSetupOutputCancelled;

    public AsyncRelayCommand StartSimpleSetupCommand { get; private set; } = null!;
    public AsyncRelayCommand CheckSimpleSlidesCommand { get; private set; } = null!;
    public bool IsAdvancedSetup { get => _isAdvancedSetup; set { if (SetProperty(ref _isAdvancedSetup, value)) OnPropertyChanged(nameof(IsSimpleSetup)); } }
    public bool IsSimpleSetup { get => !IsAdvancedSetup; set { if (value) IsAdvancedSetup = false; } }
    public bool IsSimpleSetupRunning
    {
        get => _isSimpleSetupRunning;
        private set
        {
            if (!SetProperty(ref _isSimpleSetupRunning, value)) return;
            StopOutputCommand.RaiseCanExecuteChanged();
            StartOutputCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(OutputActionText));
            OnPropertyChanged(nameof(OutputActionCommand));
            OnPropertyChanged(nameof(SimpleSetupProgressIndeterminate));
        }
    }
    public double SimpleSetupProgress { get => IsCanvaDownloadProgressVisible ? CanvaDownloadProgressPercent : _simpleSetupProgress; private set => SetProperty(ref _simpleSetupProgress, value); }
    public string SimpleSetupStatus
    {
        get => IsCanvaDownloadProgressVisible && !IsSimpleSetupRunning
            ? "Content download in progress · " + CanvaDownloadDescription + " The previous cached deck stays available until the download is ready."
            : _simpleSetupStatus;
        private set => SetProperty(ref _simpleSetupStatus, value);
    }
    public bool SimpleSetupProgressIndeterminate => (IsSimpleSetupRunning || IsCanvaDownloadProgressVisible)
        && (!IsCanvaDownloadProgressVisible || IsCanvaDownloadProgressIndeterminate);
    public bool HasPresenterAttention => _presenterSyncState != PresenterSyncState.Following;
    public string SimpleSetupDefaultsText => $"Last-used deck: {_defaultPresentationTitle ?? "None — load a deck in Advanced first"}\nConfidence display: {SelectedDisplay?.Label ?? "Unavailable — select it in Advanced"}";

    private void ConfigureSimpleSetup(AppPreferences preferences)
    {
        // Upgrade existing operators using their previously saved presentation,
        // without silently choosing a different first item from a refreshed list.
        _defaultPresentationId = Snapshot.Document.Id != "demo-sunday" ? Snapshot.Document.Id : preferences.LastAppliedPresentationId;
        _defaultPresentationTitle = Snapshot.Document.Id != "demo-sunday" ? Snapshot.Document.Title : preferences.LastAppliedPresentationTitle;
        StartSimpleSetupCommand = new AsyncRelayCommand(StartSimpleSetupAsync, () => !IsCanvaBusy && !_isClosing);
        CheckSimpleSlidesCommand = new AsyncRelayCommand(CheckSimpleSlidesAsync,
            () => !IsCanvaBusy && _canva.IsConnected && Snapshot.Document.Id != "demo-sunday");
    }

    private void RememberAppliedPresentation(CanvaPresentation presentation)
    {
        _defaultPresentationId = presentation.Id;
        _defaultPresentationTitle = presentation.Title;
        OnPropertyChanged(nameof(SimpleSetupDefaultsText));
        Persist();
    }

    internal async Task CheckSimpleSlidesAsync()
    {
        if (IsCanvaBusy || _isClosing) return;
        SimpleSetupStatus = "Checking the applied presentation for changes…";
        await RunContentRefreshActionAsync(() => CheckPresentationUpdatesCoreAsync(IsAutomaticContentRefreshEnabled));
        SimpleSetupStatus = CanvaPresentationStatus;
        SimpleSetupProgress = _contentRefreshError is null ? 100 : 0;
    }

    internal async Task StartSimpleSetupAsync()
    {
        if (IsCanvaBusy || _isClosing) return;
        long revision = ++_outputRequestRevision;
        _simpleSetupOutputCancelled = false;
        RefreshDisplays("Simple setup preflight");
        DisplayOption? target = SelectedDisplay;
        if (target is null || target.IsPrimary)
        {
            SimpleSetupStatus = "No saved Extended confidence display is available. In Windows, connect the display and choose Extend, then select it in Setup → Advanced. Simple Setup will not guess or cover the primary screen.";
            return;
        }
        if (!_canva.HasSavedConfiguration)
        {
            SimpleSetupStatus = "Canva credentials have not been saved for this Windows user. Open Settings & Diagnostics → Canva connection, save the Client ID and Secret, then return and choose Start setup.";
            return;
        }

        IsSimpleSetupRunning = true;
        SimpleSetupProgress = 0;
        bool succeeded = await RunCanvaActionAsync(async () =>
        {
            SimpleSetupStatus = "1 of 4 · Connecting to Canva. If authorization opens in your browser, approve it to continue.";
            await _canva.ConnectAsync(_lifetimeCancellation.Token);
            if (!CanContinue()) return;
            CanvaConnectionStatus = "Connected using saved Canva configuration.";
            SimpleSetupProgress = 15;
            SimpleSetupStatus = "2 of 4 · Checking the last-used presentation for changes…";
            await RefreshCanvaCoreAsync();
            if (!CanContinue()) return;
            CanvaPresentation? presentation = SimpleSetupSelection.Resolve(CanvaPresentations, _defaultPresentationId);
            if (presentation is null)
                throw new CanvaConnectException("The last-used deck is unavailable, ambiguous, or has not been set. In Setup → Advanced, Refresh the list, select the intended deck and Apply it once. Simple Setup will use that deck next time; it will not choose another deck automatically.");
            SelectedCanvaPresentation = presentation;
            presentation = await _canva.GetPresentationAsync(presentation.Id, _lifetimeCancellation.Token);
            if (!CanContinue()) return;
            SimpleSetupProgress = 25;
            bool intactImages = Snapshot.Document.Pages.All(page => page.FallbackImagePath is string path && System.IO.File.Exists(path));
            bool reused = SimpleSetupSelection.CanReuseCache(Snapshot.Document, presentation, intactImages);
            PrivacySafeLog.Write($"Simple setup content decision: reuse cache {reused}; intact page images {intactImages}; cached pages {Snapshot.Document.Pages.Count}; rendered images {Snapshot.Document.UsesRenderedPageImages}.");
            if (reused)
            {
                _lastContentCheck = DateTimeOffset.UtcNow;
                _hasPresentationFreshnessCheck = true;
                _contentRefreshError = null;
                IsPresentationUpdateAvailable = false;
                OnPropertyChanged(nameof(PresentationFreshnessText));
                CanvaPresentationStatus = $"{presentation.Title} is synced and ready. Canva has no changes since the cached download; no export was needed.";
                SimpleSetupStatus = "3 of 4 · Cached presentation is synced and ready; no download needed.";
                RememberAppliedPresentation(presentation);
            }
            else
            {
                SimpleSetupStatus = $"3 of 4 · Downloading {presentation.Title}. Content changed or the cache could not be verified. Large decks may take several minutes…";
                await LoadAndApplyCanvaCoreAsync(presentation);
            }
            if (!CanContinue()) return;
            SimpleSetupProgress = 90;
            SimpleSetupStatus = "4 of 4 · Checking the saved display and starting output…";
            RefreshDisplays("Simple setup before start");
            if (SelectedDisplay?.Id != target.Id || SelectedDisplay.IsPrimary
                || !_outputWindowManager.ShowOnDisplay(SelectedDisplay.ScreenDeviceName))
                throw new CanvaConnectException("The saved confidence display changed or disconnected during setup. The deck is cached, but no new output was started. Reconnect it and select the intended Extended display in Advanced, then Start setup again.");
            SimpleSetupProgress = 100;
            SimpleSetupStatus = $"Presentation is synced and ready; confidence output is live. {(reused ? "No changes — used the cached download." : "Updated content downloaded and cached.")} Open one Canva Presenter View; CanvaStage follows its page counter. Use Live control or Compact view during the service.";
        }, message => SimpleSetupStatus = message);
        IsSimpleSetupRunning = false;
        if (!succeeded || revision != _outputRequestRevision) SimpleSetupProgress = 0;
        // Use the existing refresh scheduling, not a second background refresh loop.
        if (succeeded && revision == _outputRequestRevision && !_isClosing)
        {
            _contentRefreshSchedule.Complete(true);
            NotifyContentRefreshHealth();
            ArmContentRefreshTimer();
        }

        bool CanContinue()
        {
            bool current = !_isClosing && revision == _outputRequestRevision;
            if (!current) PrivacySafeLog.Write("Simple setup output start suppressed: the operator stopped it or the application is closing.");
            return current;
        }
    }
}
