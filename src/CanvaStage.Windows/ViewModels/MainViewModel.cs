using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Core.Utilities;
using CanvaStage.Windows.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using BrushConverter = System.Windows.Media.BrushConverter;

namespace CanvaStage.Windows.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    public string ApplicationVersionLabel => ApplicationVersion.Label;

    private readonly IOutputCoordinator _coordinator;
    private readonly HelpLinkService _helpLinks;
    private readonly OutputWindowManager _outputWindowManager;
    private readonly DisplayService _displayService;
    private readonly Func<IReadOnlyList<DisplayOption>> _getDisplays;
    private readonly DisplayIdentificationService _displayIdentificationService;
    private readonly DiagnosticsExportService _diagnosticsExportService;
    private readonly JsonPreferencesStore _preferencesStore;
    private readonly PresentationCacheStore _presentationCacheStore;
    private readonly CanvaConnectService _canva;
    private readonly ICanvaPresenterTracker _canvaPresenterAccessibilityTracker;
    private OutputSnapshot _snapshot;
    private DisplayOption? _selectedDisplay;
    private string? _preferredDisplayId;
    private string? _preferredDisplayDeviceName;
    private bool _isRefreshingDisplays;
    private CanvaPresentation? _selectedCanvaPresentation;
    private bool _isOperatorTopmost;
    private bool _isCompactView;
    private bool _isSaving;
    private string _canvaClientId;
    private string _canvaClientSecret = string.Empty;
    private string _canvaConnectionStatus;
    private string _canvaPresentationStatus;
    private bool _isCanvaBusy;
    private string? _savedCanvaPresentationId;
    private string? _savedCanvaPresentationTitle;
    private OperatorSection _activeSection = OperatorSection.LiveControl;
    private DateTimeOffset? _lastCanvaContentRefresh;
    private bool _isCanvaDownloadProgressVisible;
    private double? _canvaDownloadProgress;
    private string _canvaDownloadDescription = string.Empty;
    private bool _isPresenterSyncEnabled;
    private string _canvaPresenterTrackingStatus = "Automatic Presenter sync is starting…";
    private bool _isPresenterSyncPaused;
    private bool _isClosing;
    private bool _isEditingCanvaSecret;
    private PresenterSyncState _presenterSyncState = PresenterSyncState.Waiting;
    private CanvaPresenterTrackingUpdate? _lastPresenterUpdate;
    private bool _isPresentationUpdateAvailable;
    private bool _hasPresentationFreshnessCheck;
    private string _diagnosticsExportStatus = "Diagnostic exports exclude credentials, tokens, and presentation content.";
    private readonly System.Windows.Threading.DispatcherTimer _presenterHealthTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly System.Windows.Threading.DispatcherTimer _contentRefreshTimer = new();
    private readonly AutomaticContentRefreshSchedule _contentRefreshSchedule;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private bool _isAutomaticContentRefreshEnabled;
    private DateTimeOffset? _lastContentCheck;
    private string? _contentRefreshError;
    private bool _isCheckingContent;
    private readonly object _progressLock = new();
    private CanvaLoadProgress? _pendingProgress;
    private bool _progressQueued;
    private bool _isAutomaticRefreshRunning;

    public MainViewModel(IOutputCoordinator coordinator, OutputWindowManager outputWindowManager, DisplayService displayService, DisplayIdentificationService displayIdentificationService, DiagnosticsExportService diagnosticsExportService, JsonPreferencesStore preferencesStore, PresentationCacheStore presentationCacheStore, CanvaConnectService canva, ICanvaPresenterTracker canvaPresenterAccessibilityTracker, AppPreferences preferences, AutomaticContentRefreshSchedule? contentRefreshSchedule = null, Func<IReadOnlyList<DisplayOption>>? getDisplays = null, HelpLinkService? helpLinks = null, Func<DisplayOption, bool>? confirmDisplayReconnect = null, DisplayMirrorService? displayMirror = null, Func<string, bool>? confirmLayoutChange = null, Func<bool>? keepMirroredLayout = null)
    {
        _coordinator = coordinator;
        _helpLinks = helpLinks ?? new HelpLinkService(ApplicationVersion.Current);
        _outputWindowManager = outputWindowManager;
        _outputWindowManager.PreviewSourceChanged += OnPreviewSourceChanged;
        _displayService = displayService;
        _getDisplays = getDisplays ?? displayService.GetDisplays;
        _confirmDisplayReconnect = confirmDisplayReconnect ?? ConfirmDisplayReconnect;
        _displayIdentificationService = displayIdentificationService;
        _diagnosticsExportService = diagnosticsExportService;
        _preferencesStore = preferencesStore;
        _presentationCacheStore = presentationCacheStore;
        _canva = canva;
        _contentRefreshSchedule = contentRefreshSchedule ?? new();
        _canvaPresenterAccessibilityTracker = canvaPresenterAccessibilityTracker;
        _snapshot = coordinator.Snapshot;
        _activeSection = _snapshot.Document.Id == "demo-sunday" ? OperatorSection.Setup : OperatorSection.LiveControl;
        _isOperatorTopmost = preferences.IsOperatorTopmost;
        _canvaClientId = canva.ConfiguredClientId ?? string.Empty;
        _savedCanvaPresentationId = preferences.SelectedPresentationId;
        _savedCanvaPresentationTitle = preferences.SelectedPresentationTitle;
        _lastCanvaContentRefresh = preferences.SelectedPresentationRefreshedAt;
        _isPresenterSyncEnabled = preferences.IsPresenterSyncEnabled;
        _isAutomaticContentRefreshEnabled = preferences.IsAutomaticContentRefreshEnabled;
        _matchSlideCapitalization = preferences.MatchSlideCapitalization;
        _canvaConnectionStatus = canva.IsConnected
            ? "Saved Canva connection is ready. Loading presentations…"
            : canva.HasSavedConfiguration
                ? "Canva is configured for this Windows user. Select Connect to authorize it."
                : "Not configured. Add the credentials from your Canva Developer Portal integration.";
        _canvaPresentationStatus = _snapshot.Document.Id == "demo-sunday"
            ? "Refresh the connected account's presentation list, then select one and Apply."
            : $"Cached {_snapshot.Document.Title} is ready for Presenter sync and manual page selection.";
        if (_snapshot.Document.Id != "demo-sunday" && _presentationCacheStore.MissingImageCountOnLoad > 0)
            _canvaPresentationStatus = $"Cached {_snapshot.Document.Title} has {_presentationCacheStore.MissingImageCountOnLoad} missing page image(s). Monitor text is available; Re-download before using Mirror.";

        Displays = new ObservableCollection<DisplayOption>(_getDisplays());
        CanvaPresentations = [];
        FontFamilies = ["Aptos", "Segoe UI", "Arial", "Verdana"];
        FontSizes = [32d, 40d, 48d, 56d, 64d, 72d, 88d, 104d, 120d, 144d, 160d];
        NextLineFontSizes = [16d, 18d, 20d, 24d, 28d, 30d, 32d, 36d, 40d, 48d, 56d, 64d];

        PreviewCommand = new RelayCommand(ShowPreview);
        OpenHelpCommand = new RelayCommand(() =>
        {
            if (_helpLinks.TryOpen() is { } error)
                System.Windows.MessageBox.Show(error, "CanvaStage Help", MessageBoxButton.OK, MessageBoxImage.Information);
        });
        StartOutputCommand = new RelayCommand(StartOutput, CanStartOutput);
        StopOutputCommand = new RelayCommand(StopOutput, () => Snapshot.IsLive || IsSimpleSetupRunning);
        BlankOutputCommand = new RelayCommand(() => _coordinator.SetBlanked(!Snapshot.IsBlanked), () => Snapshot.IsLive);
        SelectMirrorCommand = new RelayCommand(() => OutputMode = OutputMode.Mirror);
        SelectMonitorCommand = new RelayCommand(() => OutputMode = OutputMode.Monitor);
        PreviousPageCommand = new RelayCommand(() => { PauseForManualNavigation(); _coordinator.GoPrevious(); }, () => Snapshot.CanGoPrevious);
        NextPageCommand = new RelayCommand(() => { PauseForManualNavigation(); _coordinator.GoNext(); }, () => Snapshot.CanGoNext);
        TogglePresenterSyncCommand = new RelayCommand(TogglePresenterSync);
        RefreshDisplaysCommand = new RelayCommand(RefreshDisplays);
        IdentifyDisplaysCommand = new RelayCommand(IdentifyDisplays);
        TestSetupCommand = new RelayCommand(ShowSetupDiagnostics);
        OpenSetupCommand = new RelayCommand(() => ActiveSection = OperatorSection.Setup);
        OpenLiveControlCommand = new RelayCommand(() => ActiveSection = OperatorSection.LiveControl);
        ConnectCanvaCommand = new AsyncRelayCommand(ConnectCanvaAsync, () => !IsCanvaBusy);
        TestCanvaCommand = new AsyncRelayCommand(TestCanvaAsync, () => !IsCanvaBusy);
        DisconnectCanvaCommand = new AsyncRelayCommand(DisconnectCanvaAsync, () => !IsCanvaBusy && _canva.HasSavedConfiguration);
        RefreshCanvaCommand = new AsyncRelayCommand(RefreshCanvaAsync, () => !IsCanvaBusy && _canva.IsConnected);
        ApplyCanvaCommand = new AsyncRelayCommand(ApplyCanvaAsync, () => !IsCanvaBusy && SelectedCanvaPresentation is not null && _canva.IsConnected);
        RedownloadCanvaCommand = new AsyncRelayCommand(RedownloadCanvaAsync, () => !IsCanvaBusy && Snapshot.Document.Id != "demo-sunday" && _canva.IsConnected);
        CheckPresentationUpdatesCommand = new AsyncRelayCommand(CheckPresentationUpdatesAsync, () => !IsCanvaBusy && _canva.IsConnected && Snapshot.Document.Id != "demo-sunday");
        CleanOldCacheCommand = new RelayCommand(CleanOldCache, () => !IsCanvaBusy);
        ClearCacheCommand = new RelayCommand(ClearCache, () => !IsCanvaBusy);
        ExportDiagnosticsCommand = new RelayCommand(ExportDiagnostics);
        UseDemoDeckCommand = new RelayCommand(UseDemoDeck, () => !IsCanvaBusy);
        CopyRedirectUrlCommand = new RelayCommand(CopyRedirectUrl);
        OpenCanvaDeveloperPortalCommand = new RelayCommand(OpenCanvaDeveloperPortal);
        SaveCanvaConfigurationCommand = new AsyncRelayCommand(() => RunCanvaActionAsync(() =>
        {
            SaveCanvaConfiguration();
            CanvaConnectionStatus = _canva.IsConnected ? "Configuration saved securely. Canva remains connected."
                : "Configuration saved securely for this Windows account. Connect Canva to authorize it.";
            return Task.CompletedTask;
        }, message => CanvaConnectionStatus = message), () => !IsCanvaBusy);
        ReplaceCanvaSecretCommand = new RelayCommand(() => { _isEditingCanvaSecret = true; NotifySecretState(); });
        ConfigureSimpleSetup(preferences);
        ConfigureDisplayMirror(displayMirror, confirmLayoutChange, keepMirroredLayout);
        _preferredDisplayId = preferences.SelectedDisplayId;
        SelectedDisplay = DisplaySelection.Resolve(Displays, _preferredDisplayId);
        _displayReconnectPolicy.Observe(Displays, _preferredDisplayId);
        _displayReconnectTimer.Tick += OnDisplayReconnectTick;
        _outputWindowManager.DisplaysChanged += OnDisplaysChanged;
        LogDisplaySelection("Startup");
        _coordinator.SnapshotChanged += OnSnapshotChanged;
        _canva.LoadProgressChanged += OnCanvaLoadProgressChanged;
        _canvaPresenterAccessibilityTracker.TrackingUpdated += OnCanvaPresenterTrackingUpdated;
        ApplyPresenterSyncPreference();
        _presenterHealthTimer.Tick += OnPresenterHealthTick;
        _presenterHealthTimer.Start();
        _contentRefreshTimer.Tick += OnAutomaticContentRefreshTick;
        ArmContentRefreshTimer();
    }

    public ObservableCollection<DisplayOption> Displays { get; }
    public ObservableCollection<CanvaPresentation> CanvaPresentations { get; }
    public IReadOnlyList<string> FontFamilies { get; }
    public IReadOnlyList<double> FontSizes { get; }
    public IReadOnlyList<double> NextLineFontSizes { get; }
    public ICommand PreviewCommand { get; }
    public ICommand OpenHelpCommand { get; }
    public ICommand OpenSetupCommand { get; }
    public ICommand OpenLiveControlCommand { get; }
    public FrameworkElement? OutputPreviewSurface => _outputWindowManager.PreviewSurface;
    internal OutputWindowManager OutputManagerForVerification => _outputWindowManager;
    internal DisplayIdentificationService DisplayIdentificationForVerification => _displayIdentificationService;
    public RelayCommand StartOutputCommand { get; }
    public RelayCommand StopOutputCommand { get; }
    public RelayCommand BlankOutputCommand { get; }
    public RelayCommand SelectMirrorCommand { get; }
    public RelayCommand SelectMonitorCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public ICommand RefreshDisplaysCommand { get; }
    public ICommand IdentifyDisplaysCommand { get; }
    public ICommand TestSetupCommand { get; }
    public AsyncRelayCommand ConnectCanvaCommand { get; }
    public AsyncRelayCommand TestCanvaCommand { get; }
    public AsyncRelayCommand DisconnectCanvaCommand { get; }
    public AsyncRelayCommand RefreshCanvaCommand { get; }
    public AsyncRelayCommand ApplyCanvaCommand { get; }
    public AsyncRelayCommand RedownloadCanvaCommand { get; }
    public AsyncRelayCommand CheckPresentationUpdatesCommand { get; }
    public ICommand CleanOldCacheCommand { get; }
    public ICommand ClearCacheCommand { get; }
    public ICommand ExportDiagnosticsCommand { get; }
    public ICommand UseDemoDeckCommand { get; }
    public ICommand CopyRedirectUrlCommand { get; }
    public ICommand OpenCanvaDeveloperPortalCommand { get; }
    public ICommand SaveCanvaConfigurationCommand { get; }
    public ICommand ReplaceCanvaSecretCommand { get; }
    public bool HasSavedCanvaSecret => _canva.HasSavedConfiguration && string.Equals(_canva.ConfiguredClientId, CanvaClientId.Trim(), StringComparison.Ordinal);
    public bool ShowSecretInput => _isEditingCanvaSecret || !HasSavedCanvaSecret;
    public string SavedSecretDescription => HasSavedCanvaSecret
        ? "••••••••  Saved securely. Reused automatically after reopening; no need to enter it again."
        : "Enter once, then Save configuration. Stored securely for this Windows account.";
    public ICommand TogglePresenterSyncCommand { get; }
    public string PresenterSyncSummary => _presenterSyncState switch
    {
        PresenterSyncState.Following => "Following Canva",
        PresenterSyncState.Paused => "Paused — manual control",
        PresenterSyncState.Manual => "Manual control",
        PresenterSyncState.Mismatch => "Presentation mismatch",
        PresenterSyncState.Recovering => "Reconnecting to Presenter",
        _ => "Waiting for Presenter"
    };
    public Brush PresenterSyncBrush => _presenterSyncState == PresenterSyncState.Following
        ? BrushFrom("#74E5A2", Brushes.LightGreen) : BrushFrom("#E5C07B", Brushes.Goldenrod);
    public string PresenterSyncButtonText => !IsPresenterSyncEnabled || _isPresenterSyncPaused ? "Resume sync" : "Pause sync";
    public string PresenterSidebarDetail => _presenterSyncState switch
    {
        PresenterSyncState.Following => $"Following Canva page {_lastPresenterUpdate?.CurrentPage} of {_lastPresenterUpdate?.TotalPages}. Manual page changes pause following.",
        PresenterSyncState.Paused => "Manual page control is active. Choose Resume sync to follow Canva again.",
        PresenterSyncState.Manual => "Automatic following is off. Use the page controls or choose Resume sync.",
        PresenterSyncState.Mismatch => $"Canva reports {_lastPresenterUpdate?.TotalPages} pages; this deck has {Snapshot.Document.Pages.Count}. Open the matching deck or Re-download in Advanced Setup.",
        PresenterSyncState.Recovering => "Presenter scanning was interrupted and will retry. Keep Presenter View open; manual controls still work.",
        _ when Snapshot.Document.Id == "demo-sunday" => "Load your Canva deck in Setup, then open its Presenter View. The demo uses manual controls.",
        _ => "Open one matching Canva Presenter View with its page counter visible. Keep Canva and CanvaStage at the same Windows permission level."
    };
    public string RedirectUrl => CanvaConnectService.RedirectUrl;
    public bool IsCanvaPresenterTracking => _canvaPresenterAccessibilityTracker.IsTracking;
    public bool IsPresenterSyncEnabled
    {
        get => _isPresenterSyncEnabled;
        set
        {
            if (!SetProperty(ref _isPresenterSyncEnabled, value))
            {
                return;
            }

            ApplyPresenterSyncPreference();
            Persist();
        }
    }

    public string CanvaPresenterTrackingStatus
    {
        get => _canvaPresenterTrackingStatus;
        private set => SetProperty(ref _canvaPresenterTrackingStatus, value);
    }

    public string PresenterScanHealth => _presenterSyncState switch
    {
        PresenterSyncState.Following => "Healthy · receiving Presenter updates",
        PresenterSyncState.Paused => "Paused by operator",
        PresenterSyncState.Mismatch => "Attention · presentation page counts differ",
        PresenterSyncState.Recovering => "Recovering · scanner will retry automatically",
        PresenterSyncState.Manual => "Off · manual control is active",
        _ => "Waiting · looking for Canva Presenter"
    };

    public string PresenterLastDetectedText => _lastPresenterUpdate is null
        ? "No Presenter observation has been received yet."
        : $"Last scan {_lastPresenterUpdate.ObservedAt.ToLocalTime():h:mm:ss tt} · "
          + (_lastPresenterUpdate.CurrentPage is int page && _lastPresenterUpdate.TotalPages is int total ? $"page {page} of {total}" : _lastPresenterUpdate.State.ToString())
          + (_lastPresenterUpdate.ScanDuration is TimeSpan duration ? $" · {duration.TotalMilliseconds:0} ms" : string.Empty);

    public OperatorSection ActiveSection
    {
        get => _activeSection;
        set
        {
            if (!SetProperty(ref _activeSection, value)) return;
            OnPropertyChanged(nameof(IsLiveControlSelected));
            OnPropertyChanged(nameof(IsSetupSelected));
            OnPropertyChanged(nameof(IsSettingsDiagnosticsSelected));
            OnPropertyChanged(nameof(OperatorPageTitle));
            OnPropertyChanged(nameof(OperatorPageSubtitle));
        }
    }

    public bool IsLiveControlSelected { get => ActiveSection == OperatorSection.LiveControl; set { if (value) ActiveSection = OperatorSection.LiveControl; } }
    public bool IsSetupSelected { get => ActiveSection == OperatorSection.Setup; set { if (value) ActiveSection = OperatorSection.Setup; } }
    public bool IsSettingsDiagnosticsSelected { get => ActiveSection == OperatorSection.SettingsDiagnostics; set { if (value) ActiveSection = OperatorSection.SettingsDiagnostics; } }
    public string OperatorPageTitle => ActiveSection switch
    {
        OperatorSection.Setup => "Setup",
        OperatorSection.SettingsDiagnostics => "Settings & Diagnostics",
        _ => "Live control"
    };
    public string OperatorPageSubtitle => ActiveSection switch
    {
        OperatorSection.Setup => "Start with saved defaults, or use Advanced to choose a deck and display.",
        OperatorSection.SettingsDiagnostics => "Saved connection, Monitor appearance, and support tools. Usually set once.",
        _ => "Automatic Canva Presenter sync with manual controls available at all times."
    };

    public OutputSnapshot Snapshot
    {
        get => _snapshot;
        private set
        {
            if (!SetProperty(ref _snapshot, value)) return;
            OnPropertyChanged(nameof(CurrentPageNumber));
            OnPropertyChanged(nameof(PageCount));
            OnPropertyChanged(nameof(PageLabel));
            OnPropertyChanged(nameof(IsLive));
            OnPropertyChanged(nameof(OutputActionText));
            OnPropertyChanged(nameof(OutputActionCommand));
            OnPropertyChanged(nameof(IsOutputBlanked));
            OnPropertyChanged(nameof(BlankOutputButtonText));
            OnPropertyChanged(nameof(LiveStatus));
            OnPropertyChanged(nameof(OutputMode));
            OnPropertyChanged(nameof(Layout));
            OnPropertyChanged(nameof(NextLineOption));
            OnPropertyChanged(nameof(BackgroundColor));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(AccentColor));
            OnPropertyChanged(nameof(FontFamily));
            OnPropertyChanged(nameof(FontSize));
            OnPropertyChanged(nameof(NextLineFontSize));
            OnPropertyChanged(nameof(IsNextLineBold));
            OnPropertyChanged(nameof(NextLineColor));
            OnPropertyChanged(nameof(ShowClock));
            OnPropertyChanged(nameof(ShowMirrorClock));
            OnPropertyChanged(nameof(ClockFontSize));
            OnPropertyChanged(nameof(MirrorClockFontSize));
            OnPropertyChanged(nameof(IsClockBold));
            OnPropertyChanged(nameof(IsMirrorClockOnLeft));
            OnPropertyChanged(nameof(IsSlideFadeEnabled));
            OnPropertyChanged(nameof(SlideFadeMilliseconds));
            OnPropertyChanged(nameof(ContrastMessage));
            OnPropertyChanged(nameof(SelectedPage));
            OnPropertyChanged(nameof(SelectedPageTitle));
            OnPropertyChanged(nameof(PreviewPrimaryText));
            OnPropertyChanged(nameof(IsMirrorSelected));
            OnPropertyChanged(nameof(IsMonitorSelected));
            NotifyReadinessChanged();
            StartOutputCommand.RaiseCanExecuteChanged();
            StopOutputCommand.RaiseCanExecuteChanged();
            BlankOutputCommand.RaiseCanExecuteChanged();
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
        }
    }

    public int CurrentPageNumber { get => Snapshot.SelectedPageNumber; set { if (value != Snapshot.SelectedPageNumber) { PauseForManualNavigation(); _coordinator.SelectPage(value); } } }
    public int PageCount => Snapshot.Document.Pages.Count;
    public string PageLabel => $"Page {CurrentPageNumber} of {PageCount}";
    public bool IsLive => Snapshot.IsLive;
    public string OutputActionText => IsSimpleSetupRunning ? "Cancel setup start" : IsLive ? "Stop output" : "Start output";
    public ICommand OutputActionCommand => IsLive || IsSimpleSetupRunning ? StopOutputCommand : StartOutputCommand;
    public bool IsOutputBlanked => Snapshot.IsBlanked;
    public string BlankOutputButtonText => IsOutputBlanked ? "Restore output" : "Blank output";
    public string LiveStatus => IsLive ? $"{(IsOutputBlanked ? "BLANKED" : "LIVE")}  •  {Snapshot.Document.Title}  •  {PageLabel}" : $"OUTPUT OFF  •  {Snapshot.Document.Title} is ready";
    public PresentationPage? SelectedPage => Snapshot.Document.FindPage(CurrentPageNumber);
    public string SelectedPageTitle => SelectedPage?.Title ?? "No page selected";
    public string PreviewPrimaryText
    {
        get
        {
            PresentationPage? page = SelectedPage;
            if (page is null) return string.Empty;
            string text = string.Join(Environment.NewLine, page.DisplayTextBlocks.Where(static item => !string.IsNullOrWhiteSpace(item)));
            return string.IsNullOrWhiteSpace(text) ? "This page has no readable text. Monitor mode will use its Canva page image." : text;
        }
    }
    public bool IsMirrorSelected { get => OutputMode == OutputMode.Mirror; set { if (value) OutputMode = OutputMode.Mirror; } }
    public bool IsMonitorSelected { get => OutputMode == OutputMode.Monitor; set { if (value) OutputMode = OutputMode.Monitor; } }

    public DisplayOption? SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            // Clearing/replacing dropdown items can send a transient null back
            // through the binding. It must not erase the user's remembered target.
            if (_isRefreshingDisplays) return;
            if (!SetProperty(ref _selectedDisplay, value)) return;
            if (value is not null)
            {
                _preferredDisplayId = value.Id;
                _preferredDisplayDeviceName = value.ScreenDeviceName;
            }
            _outputWindowManager.SetPreviewDisplay(value?.ScreenDeviceName);
            LogDisplaySelection("Selection changed");
            Persist();
            StartOutputCommand.RaiseCanExecuteChanged();
            NotifyReadinessChanged();
            RefreshMirrorChoices();
        }
    }

    public CanvaPresentation? SelectedCanvaPresentation
    {
        get => _selectedCanvaPresentation;
        set
        {
            if (!SetProperty(ref _selectedCanvaPresentation, value)) return;
            bool selectionChanged = !string.Equals(_savedCanvaPresentationId, value?.Id, StringComparison.Ordinal);
            _savedCanvaPresentationId = value?.Id;
            _savedCanvaPresentationTitle = value?.Title;
            if (selectionChanged)
            {
                _lastCanvaContentRefresh = null;
                _hasPresentationFreshnessCheck = false;
                IsPresentationUpdateAvailable = false;
                OnPropertyChanged(nameof(LastCanvaContentRefreshText));
                OnPropertyChanged(nameof(PresentationFreshnessText));
            }
            Persist();
            ApplyCanvaCommand.RaiseCanExecuteChanged();
            NotifyReadinessChanged();
        }
    }

    // Keep the saved choice, but apply it only while Compact view is visible.
    public bool IsOperatorTopmost
    {
        get => _isOperatorTopmost;
        set
        {
            if (!SetProperty(ref _isOperatorTopmost, value)) return;
            OnPropertyChanged(nameof(IsWindowTopmost));
            Persist();
        }
    }
    public bool IsWindowTopmost => _isCompactView && IsOperatorTopmost;

    internal void SetCompactViewState(bool isCompact)
    {
        if (SetProperty(ref _isCompactView, isCompact)) OnPropertyChanged(nameof(IsWindowTopmost));
    }
    public OutputMode OutputMode { get => Snapshot.Mode; set => _coordinator.SetOutputMode(value); }
    public MonitorLayout Layout { get => Snapshot.Layout; set => _coordinator.SetLayout(value); }
    public NextLineOption NextLineOption { get => Snapshot.NextLineOption; set => _coordinator.SetNextLineOption(value); }
    public string BackgroundColor { get => Snapshot.Appearance.BackgroundColor; set => SetAppearance(Snapshot.Appearance with { BackgroundColor = value }); }
    public string TextColor { get => Snapshot.Appearance.TextColor; set => SetAppearance(Snapshot.Appearance with { TextColor = value }); }
    public string AccentColor { get => Snapshot.Appearance.AccentColor; set => SetAppearance(Snapshot.Appearance with { AccentColor = value }); }
    public string FontFamily { get => Snapshot.Appearance.FontFamily; set => SetAppearance(Snapshot.Appearance with { FontFamily = value }); }
    public double FontSize { get => Snapshot.Appearance.FontSize; set => SetAppearance(Snapshot.Appearance with { FontSize = Math.Clamp(value, 32, 160) }); }
    public double NextLineFontSize { get => Snapshot.Appearance.NextLineFontSize; set => SetAppearance(Snapshot.Appearance with { NextLineFontSize = Math.Clamp(value, 16, 64) }); }
    public bool IsNextLineBold { get => Snapshot.Appearance.IsNextLineBold; set => SetAppearance(Snapshot.Appearance with { IsNextLineBold = value }); }
    public string NextLineColor { get => Snapshot.Appearance.NextLineColor; set => SetAppearance(Snapshot.Appearance with { NextLineColor = value }); }
    public bool ShowClock { get => Snapshot.Appearance.ShowClock; set => SetAppearance(Snapshot.Appearance with { ShowClock = value }); }
    public bool ShowMirrorClock { get => Snapshot.Appearance.ShowMirrorClock; set => SetAppearance(Snapshot.Appearance with { ShowMirrorClock = value }); }
    public double ClockFontSize { get => Snapshot.Appearance.ClockFontSize; set => SetAppearance(Snapshot.Appearance with { ClockFontSize = value }); }
    public double MirrorClockFontSize { get => Snapshot.Appearance.MirrorClockFontSize; set => SetAppearance(Snapshot.Appearance with { MirrorClockFontSize = value }); }
    public bool IsClockBold { get => Snapshot.Appearance.IsClockBold; set => SetAppearance(Snapshot.Appearance with { IsClockBold = value }); }
    public bool IsMirrorClockOnLeft { get => Snapshot.Appearance.IsMirrorClockOnLeft; set => SetAppearance(Snapshot.Appearance with { IsMirrorClockOnLeft = value }); }
    public bool IsSlideFadeEnabled { get => Snapshot.Appearance.IsSlideFadeEnabled; set => SetAppearance(Snapshot.Appearance with { IsSlideFadeEnabled = value }); }
    public double SlideFadeMilliseconds { get => Snapshot.Appearance.SlideFadeMilliseconds; set => SetAppearance(Snapshot.Appearance with { SlideFadeMilliseconds = value }); }
    public string CanvaClientId { get => _canvaClientId; set { if (SetProperty(ref _canvaClientId, value)) NotifySecretState(); } }
    public string CanvaConnectionStatus
    {
        get => _canvaConnectionStatus;
        private set
        {
            if (SetProperty(ref _canvaConnectionStatus, value)) NotifyReadinessChanged();
        }
    }
    public string CanvaPresentationStatus
    {
        get => _canvaPresentationStatus;
        private set
        {
            if (SetProperty(ref _canvaPresentationStatus, value)) NotifyReadinessChanged();
        }
    }
    public bool IsCanvaDownloadProgressVisible
    {
        get => _isCanvaDownloadProgressVisible;
        private set
        {
            if (!SetProperty(ref _isCanvaDownloadProgressVisible, value)) return;
            OnPropertyChanged(nameof(SimpleSetupProgressIndeterminate));
            OnPropertyChanged(nameof(SimpleSetupProgress));
            OnPropertyChanged(nameof(SimpleSetupStatus));
        }
    }
    public bool IsCanvaDownloadProgressIndeterminate => _canvaDownloadProgress is null;
    public double CanvaDownloadProgressPercent => (_canvaDownloadProgress ?? 0d) * 100d;
    public string CanvaDownloadDescription
    {
        get => _canvaDownloadDescription;
        private set => SetProperty(ref _canvaDownloadDescription, value);
    }
    public string SelectedPresentationSummary => SelectedCanvaPresentation is not null
        ? $"{SelectedCanvaPresentation.Title} · {SelectedCanvaPresentation.PageCount} pages"
            : string.IsNullOrWhiteSpace(_savedCanvaPresentationId)
                ? "No Canva presentation selected"
            : $"Saved selection: {_savedCanvaPresentationTitle ?? "Canva presentation"}";
    public string LastCanvaContentRefreshText => _lastCanvaContentRefresh is null
        ? "No Canva presentation content loaded this session."
        : $"Content last loaded {_lastCanvaContentRefresh.Value.ToLocalTime():h:mm tt}.";
    public bool IsPresentationUpdateAvailable
    {
        get => _isPresentationUpdateAvailable;
        private set
        {
            if (!SetProperty(ref _isPresentationUpdateAvailable, value)) return;
            OnPropertyChanged(nameof(PresentationFreshnessText));
        }
    }
    public bool IsAutomaticContentRefreshEnabled
    {
        get => _isAutomaticContentRefreshEnabled;
        set
        {
            if (!SetProperty(ref _isAutomaticContentRefreshEnabled, value)) return;
            _contentRefreshSchedule.Reset();
            OnPropertyChanged(nameof(PresentationFreshnessText));
            NotifyContentRefreshHealth();
            ArmContentRefreshTimer();
            Persist();
        }
    }
    public string ContentRefreshHealthText => Snapshot.Document.Id == "demo-sunday" ? "Content refresh · Demo deck"
        : IsCanvaDownloadProgressVisible ? "Content refresh · Downloading presentation"
        : _isCheckingContent ? "Content refresh · Checking Canva"
        : !_canva.IsConnected ? "Content refresh · Reconnect Canva"
        : _contentRefreshError is not null ? "Content refresh · Needs attention"
        : !IsAutomaticContentRefreshEnabled ? "Content refresh · Automatic updates off"
        : IsCanvaBusy ? "Content refresh · Waiting for Canva action"
        : IsPresentationUpdateAvailable ? "Content refresh · Update available"
        : _lastContentCheck is not null && _hasPresentationFreshnessCheck ? "Content refresh · Checked"
        : "Content refresh · Waiting for check";
    public string ContentRefreshDetail => Snapshot.Document.Id == "demo-sunday" ? "Apply a Canva presentation to enable content checks."
        : IsCanvaDownloadProgressVisible ? "The current deck stays available until the full download is ready."
        : _isCheckingContent ? "Checking the applied presentation's modification time; unchanged content is not downloaded."
        : !_canva.IsConnected ? (_contentRefreshError is null ? "" : _contentRefreshError + " ")
            + "The cached deck remains available. Connect Canva in Setup to resume checks."
        : _contentRefreshError ?? (IsPresentationUpdateAvailable ? "A newer version is available in Canva."
            : _lastContentCheck is not null ? PresentationFreshnessText : "No content check completed for this deck this session.");
    public string ContentRefreshTimingText
    {
        get
        {
            string checkedText = _lastContentCheck is null ? "Last check: not yet"
                : $"Last check: {_lastContentCheck.Value.ToLocalTime():h:mm:ss tt}";
            string nextText = Snapshot.Document.Id == "demo-sunday" ? ""
                : !_canva.IsConnected ? " · Checks paused until reconnected"
                : !IsAutomaticContentRefreshEnabled ? " · Automatic checks off"
                : _isCheckingContent || IsCanvaDownloadProgressVisible ? " · Refresh in progress"
                : IsCanvaBusy ? " · Waiting for the current Canva action to finish"
                : $" · {(_contentRefreshError is null ? "Next check" : "Retry")}: {_contentRefreshSchedule.NextCheckAt.ToLocalTime():h:mm:ss tt}";
            return $"{checkedText} · {LastCanvaContentRefreshText}{nextText}";
        }
    }
    public Brush ContentRefreshHealthBrush => _contentRefreshError is not null || !_canva.IsConnected
        ? BrushFrom("#F5BD63", Brushes.Orange) : BrushFrom("#7AE8CB", Brushes.LightGreen);

    private void NotifyContentRefreshHealth()
    {
        OnPropertyChanged(nameof(ContentRefreshHealthText));
        OnPropertyChanged(nameof(ContentRefreshDetail));
        OnPropertyChanged(nameof(ContentRefreshTimingText));
        OnPropertyChanged(nameof(ContentRefreshHealthBrush));
    }

    private void ArmContentRefreshTimer()
    {
        _contentRefreshTimer.Stop();
        if (_isClosing || _isAutomaticRefreshRunning || !IsAutomaticContentRefreshEnabled
            || !_canva.IsConnected || Snapshot.Document.Id == "demo-sunday") return;
        // One deadline, not a fixed 30-second tick plus a second 30-second gate.
        // A busy Canva action is retried shortly without advancing the deadline.
        _contentRefreshTimer.Interval = IsCanvaBusy ? TimeSpan.FromSeconds(1)
            : TimeSpan.FromMilliseconds(Math.Max(1, _contentRefreshSchedule.Remaining.TotalMilliseconds));
        _contentRefreshTimer.Start();
    }
    public string PresentationFreshnessText => IsPresentationUpdateAvailable
        ? IsAutomaticContentRefreshEnabled ? "A newer version is available. CanvaStage will retry the content refresh automatically."
            : "A newer version is available in Canva. Select Re-download to update the local copy."
        : Snapshot.Document.Id == "demo-sunday"
            ? "Load a Canva presentation to check for updates."
            : !Snapshot.Document.UsesRenderedPageImages
                ? "This older cache uses preview images. Re-download to obtain fresh rendered Mirror images."
            : _hasPresentationFreshnessCheck
                ? "The cached copy matches the latest Canva modification time checked."
                : "Freshness is unknown for this older cache. Re-download once to enable modification checks.";
    public string CacheSummary
    {
        get
        {
            CacheStatus status = _presentationCacheStore.GetStatus();
            return status.FileCount == 0
                ? "No downloaded presentation cache."
                : $"{status.SizeText} in {status.FileCount} files · last changed {status.LastUpdatedAt?.ToLocalTime():g}";
        }
    }
    public string DiagnosticsExportStatus
    {
        get => _diagnosticsExportStatus;
        private set => SetProperty(ref _diagnosticsExportStatus, value);
    }
    public string LiveReadinessText => IsLive
        ? "Confidence output is live"
        : _canva.IsConnected && SelectedCanvaPresentation is not null
            ? "Ready — Presenter sync available"
            : "Ready — connect Canva when you are ready";
    public string CanvaDiagnostic => _canva.IsConnected ? "PASS · Canva connected" : _canva.HasSavedConfiguration ? "WARNING · Canva needs authorization" : "WARNING · Canva not configured";
    public string CanvaDiagnosticDetail => _canva.IsConnected ? "The shared Windows operator account has valid local Canva authorization." : "Open Settings & Diagnostics → Canva connection to save credentials or reconnect the church Canva integration.";
    public string PresentationDiagnostic => Snapshot.Document.Id == "demo-sunday" ? "WARNING · Demo deck loaded" : "PASS · Canva presentation loaded";
    public string PresentationDiagnosticDetail => Snapshot.Document.Id == "demo-sunday" ? "Select and Apply a Canva presentation before using live content." : $"{Snapshot.Document.Title} is available for manual page selection.";
    public string DisplayDiagnostic => SelectedDisplay is null ? "WARNING · Choose a confidence display"
        : Displays.Count > 1 ? "PASS · Confidence display available" : "WARNING · Preview-only setup";
    public string DisplayDiagnosticDetail => SelectedDisplay is null
        ? "The remembered display is unavailable or ambiguous. Reconnect it or choose a display; output will not be redirected automatically."
        : Displays.Count > 1 ? $"{SelectedDisplay.Label} is available for output."
        : "Windows currently reports one display. Preview is safe; extend displays for independent output.";
    public string PreviewDiagnostic => "PASS · Preview is available";
    public string PreviewDiagnosticDetail => "Open Preview to confirm the confidence layout without a second display.";
    public string ManualControlDiagnostic => "PASS · Manual fallback ready";
    public string ManualControlDiagnosticDetail => "Presenter sync follows Canva automatically; Previous and Next remain available as a safe manual fallback.";
    public bool IsCanvaBusy
    {
        get => _isCanvaBusy;
        private set
        {
            if (!SetProperty(ref _isCanvaBusy, value)) return;
            ConnectCanvaCommand.RaiseCanExecuteChanged();
            TestCanvaCommand.RaiseCanExecuteChanged();
            DisconnectCanvaCommand.RaiseCanExecuteChanged();
            RefreshCanvaCommand.RaiseCanExecuteChanged();
            ApplyCanvaCommand.RaiseCanExecuteChanged();
            RedownloadCanvaCommand.RaiseCanExecuteChanged();
            CheckPresentationUpdatesCommand.RaiseCanExecuteChanged();
            StartSimpleSetupCommand.RaiseCanExecuteChanged();
            CheckSimpleSlidesCommand.RaiseCanExecuteChanged();
            ((RelayCommand)CleanOldCacheCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ClearCacheCommand).RaiseCanExecuteChanged();
            ((RelayCommand)UseDemoDeckCommand).RaiseCanExecuteChanged();
            NotifyDisplayLayoutState();
        }
    }

    public string ContrastMessage
    {
        get
        {
            double textRatio = ContrastAnalyzer.GetContrastRatio(TextColor, BackgroundColor);
            double nextRatio = ContrastAnalyzer.GetContrastRatio(NextLineColor, BackgroundColor);
            return textRatio >= 4.5 && nextRatio >= 4.5
                ? $"Text {textRatio:0.0}:1 · Next {nextRatio:0.0}:1 — recommended for readable output."
                : textRatio > 0 && nextRatio > 0
                    ? $"Contrast warning: text {textRatio:0.0}:1 · next {nextRatio:0.0}:1; each should be at least 4.5:1."
                : "Contrast warning: use six-digit #RRGGBB colors.";
        }
    }

    public void SetCanvaClientSecret(string value) => _canvaClientSecret = value;
    private void NotifySecretState()
    {
        OnPropertyChanged(nameof(HasSavedCanvaSecret));
        OnPropertyChanged(nameof(ShowSecretInput));
        OnPropertyChanged(nameof(SavedSecretDescription));
    }

    private void SaveCanvaConfiguration()
    {
        _canva.SaveConfiguration(CanvaClientId, _canvaClientSecret);
        _canvaClientSecret = string.Empty;
        _isEditingCanvaSecret = false;
        NotifySecretState();
    }
    public void PrepareToClose()
    {
        _isClosing = true;
        CancelDisplayReconnectOffer();
        _displayReconnectTimer.Tick -= OnDisplayReconnectTick;
        _lifetimeCancellation.Cancel();
        _contentRefreshTimer.Stop();
        _contentRefreshTimer.Tick -= OnAutomaticContentRefreshTick;
        _presenterHealthTimer.Stop();
        _presenterHealthTimer.Tick -= OnPresenterHealthTick;
        _coordinator.SnapshotChanged -= OnSnapshotChanged;
        _canva.LoadProgressChanged -= OnCanvaLoadProgressChanged;
        _canvaPresenterAccessibilityTracker.TrackingUpdated -= OnCanvaPresenterTrackingUpdated;
        _canvaPresenterAccessibilityTracker.Dispose();
        _outputWindowManager.PreviewSourceChanged -= OnPreviewSourceChanged;
        _outputWindowManager.DisplaysChanged -= OnDisplaysChanged;
        _displayIdentificationService.Dispose();
        _outputWindowManager.Dispose();
        try
        {
            Persist();
        }
        catch (Exception exception)
        {
            PrivacySafeLog.WriteException("Preferences could not be saved while closing", exception);
        }
    }
    private void OnPreviewSourceChanged(object? sender, EventArgs args) => OnPropertyChanged(nameof(OutputPreviewSurface));
    private void ShowPreview() => _outputWindowManager.ShowPreview(this);
    private bool CanStartOutput() => SelectedDisplay is not null && !IsSimpleSetupRunning && !IsDisplayLayoutBusy && !HasMirroringRestorePoint;

    private void StartOutput()
    {
        if (!CanStartOutput()) return;
        CancelDisplayReconnectOffer();
        _outputRequestRevision++;
        RefreshDisplays("Before Start output");
        LogDisplaySelection("Start output");
        // Route the current selection by its exact Windows device, not by saved identity.
        if (!_outputWindowManager.ShowOnDisplay(SelectedDisplay?.ScreenDeviceName))
            System.Windows.MessageBox.Show("That display is no longer available. Refresh the display list and choose another target.", "Display unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
        else _simpleSetupOutputCancelled = false;
    }

    private void StopOutput()
    {
        CancelDisplayReconnectOffer();
        _outputRequestRevision++;
        _simpleSetupOutputCancelled = true;
        if (IsSimpleSetupRunning) SimpleSetupStatus = "Setup start cancelled. The current Canva action may finish caching, but output will stay off. Choose Start setup when ready.";
        else SimpleSetupStatus = "Output stopped. The cached presentation remains available. Choose Start setup to check it and start again.";
        _outputWindowManager.StopOutput();
    }

    private void ApplyPresenterSyncPreference()
    {
        _isPresenterSyncPaused = false;
        _lastPresenterUpdate = null;
        if (IsPresenterSyncEnabled)
        {
            _canvaPresenterAccessibilityTracker.Start();
            SetPresenterState(PresenterSyncState.Waiting, "Open one Canva Presenter View on this computer, with its page counter visible. CanvaStage follows it automatically when its visible-page count matches the loaded deck. Use Setup to load the deck; manual pages remain available.");
        }
        else
        {
            _canvaPresenterAccessibilityTracker.Stop();
            SetPresenterState(PresenterSyncState.Manual, "Automatic Presenter sync is off. Use manual page controls or Resume sync.");
        }

        OnPropertyChanged(nameof(IsCanvaPresenterTracking));
    }
    private void RefreshDisplays() => RefreshDisplays("Manual refresh");

    private void RefreshDisplays(string reason) => ApplyDisplaySnapshot(_getDisplays(), reason);

    private void IdentifyDisplays()
    {
        RefreshDisplays("Before Identify displays");
        _displayIdentificationService.Show();
    }

    private void OnDisplaysChanged(object? sender, EventArgs args)
    {
        if (!_isClosing) RefreshDisplays("Windows display change");
    }

    internal void ApplyDisplaySnapshot(IReadOnlyList<DisplayOption> available, string reason)
    {
        DisplayOption? selection = DisplaySelection.Resolve(available, _preferredDisplayId, _preferredDisplayDeviceName);
        _isRefreshingDisplays = true;
        try
        {
            Displays.Clear();
            foreach (DisplayOption display in available) Displays.Add(display);
        }
        finally { _isRefreshingDisplays = false; }
        SelectedDisplay = selection;
        // Rebind even when record equality says the selection has not changed:
        // ItemsSource reset may have cleared the ComboBox's SelectedItem.
        OnPropertyChanged(nameof(SelectedDisplay));
        StartOutputCommand.RaiseCanExecuteChanged();
        LogDisplaySelection(reason);
        NotifyReadinessChanged();
        ObserveDisplayReconnect(available);
        RefreshMirrorChoices();
    }

    private void LogDisplaySelection(string reason)
    {
        string available = string.Join("; ", Displays.Select(display => $"{display.ScreenDeviceName} id {display.Id} {display.Label}"));
        PrivacySafeLog.Write($"Display selection ({reason}): selected {SelectedDisplay?.ScreenDeviceName ?? "none"}; remembered id {_preferredDisplayId ?? "none"}; remembered device {_preferredDisplayDeviceName ?? "none"}; available {available}.");
    }

    private void ShowSetupDiagnostics()
    {
        RefreshDisplays();
        ActiveSection = OperatorSection.SettingsDiagnostics;
        NotifyReadinessChanged();
    }

    private async Task ConnectCanvaAsync() => await RunCanvaActionAsync(async () =>
    {
        CanvaConnectionStatus = "Connecting to Canva using saved authorization when available…";
        SaveCanvaConfiguration();
        await _canva.ConnectAsync(_lifetimeCancellation.Token);
        _canvaClientSecret = string.Empty;
        CanvaConnectionStatus = "Connected. Refreshing presentations…";
        await RefreshCanvaCoreAsync();
    }, message => CanvaConnectionStatus = message);

    private async Task TestCanvaAsync() => await RunCanvaActionAsync(async () =>
    {
        SaveCanvaConfiguration();
        CanvaConnectionStatus = "Testing the Canva connection…";
        await _canva.TestAsync(_lifetimeCancellation.Token);
        _canvaClientSecret = string.Empty;
        CanvaConnectionStatus = "Connection test passed. Refresh presentations to choose one.";
    }, message => CanvaConnectionStatus = message);

    private async Task DisconnectCanvaAsync() => await RunCanvaActionAsync(async () =>
    {
        CanvaConnectionStatus = "Disconnecting Canva and removing local protected credentials…";
        await _canva.DisconnectAsync(_lifetimeCancellation.Token);
        _canvaClientId = string.Empty;
        _canvaClientSecret = string.Empty;
        OnPropertyChanged(nameof(CanvaClientId));
        CanvaPresentations.Clear();
        SelectedCanvaPresentation = null;
        _savedCanvaPresentationTitle = null;
        CanvaConnectionStatus = "Disconnected. Local Canva credentials were removed.";
        CanvaPresentationStatus = "Canva presentations are no longer available. You can continue with the local demo deck.";
    }, message => CanvaConnectionStatus = message);

    private async Task RefreshCanvaAsync() => await RunCanvaActionAsync(RefreshCanvaCoreAsync, message => CanvaPresentationStatus = message);

    private async Task RefreshCanvaCoreAsync()
    {
        CanvaPresentationStatus = "Refreshing Canva presentations…";
        IReadOnlyList<CanvaPresentation> presentations = await _canva.ListPresentationsAsync(_lifetimeCancellation.Token);
        if (_isClosing) return;
        CanvaPresentations.Clear();
        foreach (CanvaPresentation presentation in presentations) CanvaPresentations.Add(presentation);
        SelectedCanvaPresentation = CanvaPresentations.FirstOrDefault(presentation => presentation.Id == _savedCanvaPresentationId)
            ?? CanvaPresentations.FirstOrDefault(presentation => presentation.Id == Snapshot.Document.Id)
            ?? CanvaPresentations.FirstOrDefault();
        EvaluatePresentationFreshness();
        CanvaPresentationStatus = presentations.Count == 0 ? "Canva is connected, but no presentations were found for this account." : $"Found {presentations.Count} Canva presentation{(presentations.Count == 1 ? string.Empty : "s")}. Select one and Apply.";
    }

    private async Task ApplyCanvaAsync()
    {
        CanvaPresentation? presentation = SelectedCanvaPresentation;
        if (presentation is null) return;
        await RunContentRefreshActionAsync(() => LoadAndApplyCanvaCoreAsync(presentation));
    }

    internal async Task RedownloadCanvaAsync()
    {
        PresentationDocument current = Snapshot.Document;
        if (current.Id == "demo-sunday") return;
        PrivacySafeLog.Write("Canva manual Re-download requested for the applied deck.");
        await RunContentRefreshActionAsync(() => LoadAndApplyCanvaCoreAsync(
            new CanvaPresentation(current.Id, current.Title, current.Pages.Count), current));
    }

    private async Task LoadAndApplyCanvaCoreAsync(CanvaPresentation presentation, PresentationDocument? expectedDocument = null)
    {
        IsCanvaDownloadProgressVisible = true;
        NotifyContentRefreshHealth();
        _canvaDownloadProgress = null;
        CanvaDownloadDescription = "Preparing Canva presentation…";
        OnPropertyChanged(nameof(IsCanvaDownloadProgressIndeterminate));
        OnPropertyChanged(nameof(CanvaDownloadProgressPercent));
        OnPropertyChanged(nameof(SimpleSetupProgress));
        OnPropertyChanged(nameof(SimpleSetupStatus));
        OnPropertyChanged(nameof(SimpleSetupProgressIndeterminate));
        try
        {
            CanvaPresentationStatus = $"Preparing and reading {presentation.Title}. Text and Mirror image exports can take several minutes.";
            bool matchCapitalization = MatchSlideCapitalization;
            PresentationDocument imported = await Task.Run(() => _canva.LoadAsync(presentation.Id, matchCapitalization, _lifetimeCancellation.Token), _lifetimeCancellation.Token);
            PresentationDocument document = imported.WithSlideCapitalization(MatchSlideCapitalization);
            if (_isClosing || expectedDocument is not null && (!ReferenceEquals(Snapshot.Document, expectedDocument) || !IsAutomaticContentRefreshEnabled && _isAutomaticRefreshRunning)) return;
            await Task.Run(() => _presentationCacheStore.Save(document), _lifetimeCancellation.Token);
            if (_isClosing) return;
            int? presenterPage = IsPresenterSyncEnabled && !_isPresenterSyncPaused
                && _lastPresenterUpdate is { State: CanvaPresenterTrackingState.PageDetected, CurrentPage: int page, TotalPages: int total }
                && total == document.Pages.Count && DateTimeOffset.UtcNow - _lastPresenterUpdate.ObservedAt <= TimeSpan.FromSeconds(3)
                ? page : null;
            _coordinator.SetDocument(document.WithSlideCapitalization(MatchSlideCapitalization), presenterPage);
            RememberAppliedPresentation(presentation);
            PrivacySafeLog.Write($"Canva content applied: {document.Pages.Count} visible pages; selected page {Snapshot.SelectedPageNumber}; rendered images {document.UsesRenderedPageImages}; source modified {DiagnosticTimestamp.FormatUtc(document.SourceModifiedAt)}.");
            _lastCanvaContentRefresh = document.RefreshedAt;
            OnPropertyChanged(nameof(LastCanvaContentRefreshText));
            IsPresentationUpdateAvailable = false;
            _hasPresentationFreshnessCheck = document.SourceModifiedAt is not null;
            OnPropertyChanged(nameof(CacheSummary));
            OnPropertyChanged(nameof(PresentationFreshnessText));
            int missingImages = document.Pages.Count(page => string.IsNullOrWhiteSpace(page.FallbackImagePath));
            CanvaPresentationStatus = missingImages == 0
                ? $"{document.Title} is ready and cached locally."
                : $"{document.Title} is ready, but {missingImages} page image(s) could not be cached. Monitor text is available; Re-download before relying on Mirror.";
            Persist();
        }
        finally
        {
            IsCanvaDownloadProgressVisible = false;
            NotifyContentRefreshHealth();
        }
    }

    private void UseDemoDeck()
    {
        _coordinator.SetDocument(DemoPresentationProvider.Create());
        _lastCanvaContentRefresh = null;
        OnPropertyChanged(nameof(LastCanvaContentRefreshText));
        IsPresentationUpdateAvailable = false;
        _hasPresentationFreshnessCheck = false;
        OnPropertyChanged(nameof(CacheSummary));
        OnPropertyChanged(nameof(PresentationFreshnessText));
        CanvaPresentationStatus = "The local demo deck is ready. Canva remains connected.";
        Persist();
    }

    public async Task InitializeCanvaAsync()
    {
        if (!_canva.IsConnected || _isClosing) return;
        bool succeeded = await RunCanvaActionAsync(async () =>
        {
            await RefreshCanvaCoreAsync();
            CanvaConnectionStatus = "Connected using saved authorization. Presentations loaded automatically.";
        }, message => { CanvaConnectionStatus = message; CanvaPresentationStatus = message; });
        if (succeeded) await CheckAutomaticContentRefreshAsync(true);
    }

    internal async Task CheckPresentationUpdatesAsync() => await RunContentRefreshActionAsync(
        () => CheckPresentationUpdatesCoreAsync(IsAutomaticContentRefreshEnabled, forceDownload: IsAutomaticContentRefreshEnabled));

    private async Task RunContentRefreshActionAsync(Func<Task> action)
    {
        if (_isClosing || IsCanvaBusy) return;
        _contentRefreshError = null;
        bool succeeded = await RunCanvaActionAsync(action, message =>
        {
            _contentRefreshError = message + " The current presentation remains available.";
            CanvaPresentationStatus = _contentRefreshError;
        });
        _contentRefreshSchedule.Complete(succeeded);
        NotifyContentRefreshHealth();
        ArmContentRefreshTimer();
    }

    private async Task CheckPresentationUpdatesCoreAsync(bool applyUpdates, bool forceDownload = false)
    {
        PresentationDocument applied = Snapshot.Document;
        if (applied.Id == "demo-sunday") return;
        _isCheckingContent = true;
        NotifyContentRefreshHealth();
        try
        {
            CanvaPresentation current = await _canva.GetPresentationAsync(applied.Id, _lifetimeCancellation.Token);
            if (_isClosing || !ReferenceEquals(Snapshot.Document, applied)) return;
            _lastContentCheck = DateTimeOffset.UtcNow;
            PrivacySafeLog.Write($"Canva content check: cached modified {DiagnosticTimestamp.FormatUtc(applied.SourceModifiedAt)}; reported modified {DiagnosticTimestamp.FormatUtc(current.ModifiedAt)}; automatic refresh {_isAutomaticRefreshRunning}; force download {forceDownload}; rendered images {applied.UsesRenderedPageImages}.");
            CanvaPresentation? listed = CanvaPresentations.FirstOrDefault(item => item.Id == current.Id);
            if (listed is not null)
            {
                int index = CanvaPresentations.IndexOf(listed);
                CanvaPresentations[index] = current;
                if (SelectedCanvaPresentation == listed) SelectedCanvaPresentation = current;
            }
            EvaluatePresentationFreshness(current);
            if (applyUpdates && (forceDownload || !applied.UsesRenderedPageImages || IsPresentationUpdateAvailable || applied.SourceModifiedAt is null && current.ModifiedAt is not null))
            {
                await LoadAndApplyCanvaCoreAsync(current, applied);
                return;
            }
            CanvaPresentationStatus = IsPresentationUpdateAvailable
                ? "Canva has a newer version. Select Re-download to update the cached presentation."
                : !applied.UsesRenderedPageImages ? "This older cache uses preview images. Select Re-download to obtain fresh Mirror images."
                : _hasPresentationFreshnessCheck ? "The cached copy matches Canva's reported modification time."
                    : "Canva did not provide comparable edit timestamps. Use Re-download to refresh the presentation.";
        }
        finally
        {
            _isCheckingContent = false;
            NotifyContentRefreshHealth();
        }
    }

    private async void OnAutomaticContentRefreshTick(object? sender, EventArgs eventArgs)
    {
        _contentRefreshTimer.Stop();
        await CheckAutomaticContentRefreshAsync();
    }

    internal async Task CheckAutomaticContentRefreshAsync(bool force = false)
    {
        if (_isClosing || !IsAutomaticContentRefreshEnabled || !_canva.IsConnected
            || IsCanvaBusy || Snapshot.Document.Id == "demo-sunday" || !force && !_contentRefreshSchedule.IsDue)
        {
            ArmContentRefreshTimer();
            return;
        }
        _contentRefreshTimer.Stop();
        _isAutomaticRefreshRunning = true;
        try
        {
            await RunContentRefreshActionAsync(() => CheckPresentationUpdatesCoreAsync(true));
        }
        finally
        {
            _isAutomaticRefreshRunning = false;
            ArmContentRefreshTimer();
        }
    }

    private void EvaluatePresentationFreshness(CanvaPresentation? checkedPresentation = null)
    {
        CanvaPresentation? presentation = checkedPresentation ?? SelectedCanvaPresentation;
        _hasPresentationFreshnessCheck = presentation is not null
            && presentation.Id == Snapshot.Document.Id
            && presentation.ModifiedAt is not null
            && Snapshot.Document.SourceModifiedAt is not null;
        IsPresentationUpdateAvailable = presentation is not null
            && presentation.Id == Snapshot.Document.Id
            && presentation.ModifiedAt is DateTimeOffset modifiedAt
            && Snapshot.Document.SourceModifiedAt is DateTimeOffset cachedAt
            && modifiedAt > cachedAt;
        OnPropertyChanged(nameof(PresentationFreshnessText));
    }

    private void CleanOldCache()
    {
        try
        {
            int removed = _presentationCacheStore.RemoveObsoleteGenerations(Snapshot.Document);
            CanvaPresentationStatus = removed == 0 ? "No old download generations needed cleanup." : $"Removed {removed} old presentation download generation{(removed == 1 ? string.Empty : "s")}.";
            OnPropertyChanged(nameof(CacheSummary));
        }
        catch (Exception exception)
        {
            PrivacySafeLog.WriteException("Old cache cleanup failed", exception);
            CanvaPresentationStatus = "Old cached downloads could not be removed. The active presentation was not changed.";
        }
    }

    private void ClearCache()
    {
        MessageBoxResult result = System.Windows.MessageBox.Show(
            "Remove the downloaded presentation and all cached page images? Canva credentials and preferences will be kept.",
            "Clear downloaded presentation cache?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            _outputWindowManager.StopOutput();
            _coordinator.SetDocument(DemoPresentationProvider.Create());
            _presentationCacheStore.Clear();
            _lastCanvaContentRefresh = null;
            IsPresentationUpdateAvailable = false;
            _hasPresentationFreshnessCheck = false;
            OnPropertyChanged(nameof(LastCanvaContentRefreshText));
            OnPropertyChanged(nameof(CacheSummary));
            OnPropertyChanged(nameof(PresentationFreshnessText));
            CanvaPresentationStatus = "Downloaded presentation cache cleared. Canva credentials and the saved presentation selection were kept.";
            Persist();
        }
        catch (Exception exception)
        {
            PrivacySafeLog.WriteException("Presentation cache clear failed", exception);
            CanvaPresentationStatus = "The presentation cache could not be fully cleared. Close any preview window and try again.";
        }
    }

    private void ExportDiagnostics()
    {
        try
        {
            string path = _diagnosticsExportService.Export(new DiagnosticsSnapshot(
                PageCount,
                CurrentPageNumber,
                IsLive,
                PresenterScanHealth,
                _lastPresenterUpdate is null ? null : PresenterLastDetectedText,
                CacheSummary,
                Displays.Select(display => $"{display.Label} · Windows device {display.ScreenDeviceName}").ToArray(),
                SelectedDisplay is null ? $"Unavailable; remembered id {_preferredDisplayId ?? "none"}"
                    : $"{SelectedDisplay.Label} · Windows device {SelectedDisplay.ScreenDeviceName}"));
            DiagnosticsExportStatus = $"Saved sanitized diagnostics to {path}";
        }
        catch (Exception exception)
        {
            PrivacySafeLog.WriteException("Diagnostic export failed", exception);
            DiagnosticsExportStatus = "Diagnostics could not be exported to the desktop. Check Windows folder permissions and try again.";
        }
    }
    private void CopyRedirectUrl() { System.Windows.Clipboard.SetText(RedirectUrl); CanvaConnectionStatus = "The redirect URL was copied. Add it under Authorized redirects in your Canva Developer Portal integration."; }
    private void OpenCanvaDeveloperPortal()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.canva.dev/") { UseShellExecute = true });
            CanvaConnectionStatus = "The Canva Developer Portal was opened in your browser.";
        }
        catch (Exception)
        {
            CanvaConnectionStatus = "CanvaStage could not open the browser. Visit canva.dev to manage the integration.";
        }
    }

    private async Task<bool> RunCanvaActionAsync(Func<Task> action, Action<string> setFailureStatus)
    {
        if (IsCanvaBusy || _isClosing) return false;
        IsCanvaBusy = true;
        bool succeeded = false;
        try { await action(); succeeded = true; }
        catch (CanvaAuthorizationRequiredException exception) { PrivacySafeLog.Write("Canva authorization requires operator attention."); CanvaConnectionStatus = exception.Message; setFailureStatus(exception.Message); }
        catch (CanvaConnectException exception) { PrivacySafeLog.Write($"Canva action failed: {exception.Message}"); setFailureStatus(exception.Message); }
        catch (HttpRequestException) { PrivacySafeLog.Write("Canva request could not reach the service."); setFailureStatus("Canva could not be reached. Check the internet connection and try again."); }
        catch (OperationCanceledException) { PrivacySafeLog.Write("Canva action was cancelled."); if (!_isClosing) setFailureStatus("The Canva action was cancelled."); }
        catch (System.Text.Json.JsonException) { PrivacySafeLog.Write("Canva returned unreadable JSON data."); setFailureStatus("Canva returned data CanvaStage could not read. Try the action again; the selected presentation was not changed."); }
        catch (System.IO.InvalidDataException) { PrivacySafeLog.Write("Canva returned an unreadable presentation export."); setFailureStatus("CanvaStage could not read the PowerPoint export. Try Apply again; the previous presentation remains available."); }
        catch (System.IO.IOException exception) { PrivacySafeLog.WriteException("Canva presentation storage failed", exception); setFailureStatus("CanvaStage could not save the downloaded presentation. Check free disk space and try Apply again. The previous presentation remains available."); }
        catch (UnauthorizedAccessException exception) { PrivacySafeLog.WriteException("Canva storage access denied", exception); setFailureStatus("Windows blocked access to CanvaStage's local storage. Check folder permissions and try again."); }
        catch (Exception exception) { PrivacySafeLog.Write($"Canva action failed unexpectedly ({exception.GetType().Name})."); setFailureStatus("CanvaStage could not finish this Canva action. Try again; if it repeats, export diagnostics from Settings & Diagnostics."); }
        finally { IsCanvaBusy = false; NotifyReadinessChanged(); }
        return succeeded;
    }

    private static Brush BrushFrom(string value, Brush fallback)
    {
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(value)!;
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    private void OnCanvaLoadProgressChanged(object? sender, CanvaLoadProgress progress)
    {
        System.Windows.Threading.Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (_isClosing || dispatcher is null || dispatcher.HasShutdownStarted) return;
        lock (_progressLock)
        {
            _pendingProgress = progress;
            if (_progressQueued) return;
            _progressQueued = true;
        }
        _ = dispatcher.InvokeAsync(() =>
        {
            CanvaLoadProgress? latest;
            lock (_progressLock)
            {
                latest = _pendingProgress;
                _pendingProgress = null;
                _progressQueued = false;
            }
            if (!_isClosing && latest is not null) ApplyCanvaLoadProgress(latest);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ApplyCanvaLoadProgress(CanvaLoadProgress progress)
    {
        _canvaDownloadProgress = progress.Fraction;
        CanvaDownloadDescription = progress.Message;
        OnPropertyChanged(nameof(IsCanvaDownloadProgressIndeterminate));
        OnPropertyChanged(nameof(CanvaDownloadProgressPercent));
        OnPropertyChanged(nameof(SimpleSetupProgress));
        OnPropertyChanged(nameof(SimpleSetupStatus));
        OnPropertyChanged(nameof(SimpleSetupProgressIndeterminate));
        if (IsSimpleSetupRunning && !_simpleSetupOutputCancelled)
        {
            // Canva preparation has no measurable percentage; do not invent one.
            // Download phases retain their actual byte/page progress in the UI.
            SimpleSetupStatus = "3 of 4 · " + progress.Message;
            if (progress.Fraction is double fraction) SimpleSetupProgress = 100 * Math.Clamp(fraction, 0, 1);
            OnPropertyChanged(nameof(SimpleSetupProgressIndeterminate));
        }
    }

    private async void OnCanvaPresenterTrackingUpdated(object? sender, CanvaPresenterTrackingUpdate update)
    {
        try
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ApplyCanvaPresenterTrackingUpdate(update));
        }
        catch (Exception exception)
        {
            PrivacySafeLog.WriteException("Canva presenter accessibility update could not be applied", exception);
        }
    }

    private void ApplyCanvaPresenterTrackingUpdate(CanvaPresenterTrackingUpdate update)
    {
        if (_isClosing || !IsPresenterSyncEnabled || update.Generation != _canvaPresenterAccessibilityTracker.Generation) return;
        if (_lastPresenterUpdate is null || _lastPresenterUpdate.State != update.State
            || _lastPresenterUpdate.CurrentPage != update.CurrentPage || _lastPresenterUpdate.TotalPages != update.TotalPages)
            PrivacySafeLog.Write($"Presenter observation: {update.State}; page {update.CurrentPage?.ToString() ?? "none"}; visible pages {update.TotalPages?.ToString() ?? "none"}; observed {DiagnosticTimestamp.FormatUtc(update.ObservedAt)}; scan ms {update.ScanDuration?.TotalMilliseconds:0}; paused {_isPresenterSyncPaused}.");
        _lastPresenterUpdate = update;
        OnPropertyChanged(nameof(PresenterLastDetectedText));
        ReevaluatePresenterSync();
    }

    private void ReevaluatePresenterSync()
    {
        OnPropertyChanged(nameof(IsCanvaPresenterTracking));
        if (!IsPresenterSyncEnabled)
        {
            SetPresenterState(PresenterSyncState.Manual, "Automatic Presenter sync is off. Use manual page controls or Resume sync.");
            return;
        }
        if (_isPresenterSyncPaused)
        {
            SetPresenterState(PresenterSyncState.Paused, "Sync is paused. Manual page changes stay in place until you choose Resume sync.");
            return;
        }
        CanvaPresenterTrackingUpdate? update = _lastPresenterUpdate;
        if (update is null || DateTimeOffset.UtcNow - update.ObservedAt > TimeSpan.FromSeconds(3))
        {
            SetPresenterState(PresenterSyncState.Waiting, "No fresh Canva Presenter counter has been received. Open one Canva Presenter View on this computer, keep its page counter visible, and run Canva and CanvaStage at the same Windows privilege level. Manual page controls remain available.");
            return;
        }
        if (update.State != CanvaPresenterTrackingState.PageDetected)
        {
            SetPresenterState(update.State == CanvaPresenterTrackingState.Error ? PresenterSyncState.Recovering : PresenterSyncState.Waiting,
                update.Message + " Open one Canva Presenter View with its page counter visible, at the same Windows privilege level as CanvaStage. The scanner retries automatically; use manual pages while waiting. If it persists, export diagnostics in Settings & Diagnostics.");
            return;
        }
        if (Snapshot.Document.Id == "demo-sunday")
        {
            SetPresenterState(PresenterSyncState.Waiting, "Load a Canva presentation to enable Presenter sync. The demo uses manual controls.");
            return;
        }
        if (update.CurrentPage is null || update.TotalPages is null)
        {
            SetPresenterState(PresenterSyncState.Waiting, "Canva Presenter has not exposed a complete page counter. Keep one Presenter View open with the counter visible. Use manual page controls until it can be read.");
            return;
        }
        int pageCount = Snapshot.Document.Pages.Count;
        if (update.TotalPages.Value != pageCount)
        {
            SetPresenterState(PresenterSyncState.Mismatch,
                $"Canva Presenter reports {update.TotalPages.Value} visible pages; the loaded deck ‘{Snapshot.Document.Title}’ has {pageCount}. They may be different decks, or slides/hidden-slide settings changed after download. In Setup → Advanced, select the matching deck and Apply (or Re-download the current deck). Close other Presenter Views. The last page is held safely; manual pages remain available.");
            return;
        }
        if (Snapshot.Document.FindPage(update.CurrentPage.Value) is null)
        {
            SetPresenterState(PresenterSyncState.Mismatch, $"Canva reports page {update.CurrentPage.Value}, which is outside the loaded deck's {pageCount} pages. Open the matching Presenter View and Re-download in Setup → Advanced if slides changed. The last page is held safely; manual pages remain available.");
            return;
        }
        if (Snapshot.SelectedPageNumber != update.CurrentPage.Value)
        {
            _coordinator.SelectPage(update.CurrentPage.Value);
            PrivacySafeLog.Write($"Presenter page selected: requested {update.CurrentPage.Value}; applied {Snapshot.SelectedPageNumber}; mode {Snapshot.Mode}; live {Snapshot.IsLive}.");
        }
        SetPresenterState(PresenterSyncState.Following,
            $"Synced to presenter page {update.CurrentPage.Value} at {update.ObservedAt.ToLocalTime():h:mm:ss tt}.");
    }

    private void OnPresenterHealthTick(object? sender, EventArgs eventArgs)
    {
        OnPropertyChanged(nameof(PresenterLastDetectedText));
        if (_presenterSyncState == PresenterSyncState.Following && _lastPresenterUpdate is not null
            && DateTimeOffset.UtcNow - _lastPresenterUpdate.ObservedAt > TimeSpan.FromSeconds(3))
            ReevaluatePresenterSync();
    }

    private void SetPresenterState(PresenterSyncState state, string message)
    {
        if (_presenterSyncState != state)
        {
            string detectedPage = _lastPresenterUpdate?.CurrentPage?.ToString() ?? "none";
            string detectedCount = _lastPresenterUpdate?.TotalPages?.ToString() ?? "none";
            PrivacySafeLog.Write($"Presenter sync state: {state}. Detected page: {detectedPage}; visible pages: {detectedCount}; applied pages: {Snapshot.Document.Pages.Count}.");
        }
        _presenterSyncState = state;
        CanvaPresenterTrackingStatus = message;
        OnPropertyChanged(nameof(PresenterSyncSummary));
        OnPropertyChanged(nameof(PresenterSyncBrush));
        OnPropertyChanged(nameof(PresenterSyncButtonText));
        OnPropertyChanged(nameof(PresenterSidebarDetail));
        OnPropertyChanged(nameof(PresenterScanHealth));
        OnPropertyChanged(nameof(HasPresenterAttention));
    }

    private void PauseForManualNavigation()
    {
        if (!IsPresenterSyncEnabled) return;
        _isPresenterSyncPaused = true;
        ReevaluatePresenterSync();
    }

    private void TogglePresenterSync()
    {
        if (!IsPresenterSyncEnabled) { IsPresenterSyncEnabled = true; return; }
        _isPresenterSyncPaused = !_isPresenterSyncPaused;
        ReevaluatePresenterSync();
    }

    internal void ReceivePresenterUpdateForVerification(CanvaPresenterTrackingUpdate update)
        => ApplyCanvaPresenterTrackingUpdate(update with { Generation = _canvaPresenterAccessibilityTracker.Generation });

    private void SetAppearance(MonitorAppearance appearance) => _coordinator.SetAppearance(appearance);
    private void OnSnapshotChanged(object? sender, OutputSnapshot snapshot)
    {
        bool documentChanged = !ReferenceEquals(Snapshot.Document, snapshot.Document);
        if (Snapshot.Document.Id != snapshot.Document.Id)
        {
            _lastContentCheck = null;
            _contentRefreshError = null;
            _contentRefreshSchedule.Reset();
        }
        Snapshot = snapshot;
        if (documentChanged)
        {
            OnPropertyChanged(nameof(SlideCapitalizationStatus));
            ReevaluatePresenterSync();
            EvaluatePresentationFreshness();
            OnPropertyChanged(nameof(CacheSummary));
            NotifyContentRefreshHealth();
            ArmContentRefreshTimer();
        }
        Persist();
    }

    private void NotifyReadinessChanged()
    {
        NotifyContentRefreshHealth();
        ArmContentRefreshTimer();
        NotifySecretState();
        OnPropertyChanged(nameof(SelectedPresentationSummary));
        OnPropertyChanged(nameof(LiveReadinessText));
        OnPropertyChanged(nameof(CanvaDiagnostic));
        OnPropertyChanged(nameof(CanvaDiagnosticDetail));
        OnPropertyChanged(nameof(PresentationDiagnostic));
        OnPropertyChanged(nameof(PresentationDiagnosticDetail));
        OnPropertyChanged(nameof(DisplayDiagnostic));
        OnPropertyChanged(nameof(DisplayDiagnosticDetail));
        OnPropertyChanged(nameof(PreviewDiagnostic));
        OnPropertyChanged(nameof(PreviewDiagnosticDetail));
        OnPropertyChanged(nameof(ManualControlDiagnostic));
        OnPropertyChanged(nameof(ManualControlDiagnosticDetail));
        CheckSimpleSlidesCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(SimpleSetupDefaultsText));
    }

    private void Persist()
    {
        if (_isSaving) return;
        _isSaving = true;
        try
        {
            _preferencesStore.Save(new AppPreferences(_preferredDisplayId, CurrentPageNumber, Snapshot.Mode, Snapshot.Layout, Snapshot.NextLineOption, Snapshot.Appearance, IsOperatorTopmost, SelectedCanvaPresentation?.Id ?? _savedCanvaPresentationId, SelectedCanvaPresentation?.Title ?? _savedCanvaPresentationTitle, _lastCanvaContentRefresh, IsPresenterSyncEnabled, IsAutomaticContentRefreshEnabled, _defaultPresentationId, _defaultPresentationTitle, MatchSlideCapitalization));
        }
        finally { _isSaving = false; }
    }
}
