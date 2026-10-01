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

public sealed class MainViewModel : ViewModelBase
{
    private readonly IOutputCoordinator _coordinator;
    private readonly OutputWindowManager _outputWindowManager;
    private readonly DisplayService _displayService;
    private readonly DisplayIdentificationService _displayIdentificationService;
    private readonly DiagnosticsExportService _diagnosticsExportService;
    private readonly JsonPreferencesStore _preferencesStore;
    private readonly PresentationCacheStore _presentationCacheStore;
    private readonly CanvaConnectService _canva;
    private readonly ICanvaPresenterTracker _canvaPresenterAccessibilityTracker;
    private OutputSnapshot _snapshot;
    private DisplayOption? _selectedDisplay;
    private CanvaPresentation? _selectedCanvaPresentation;
    private bool _isOperatorTopmost;
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
    private readonly System.Windows.Threading.DispatcherTimer _contentRefreshTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private bool _isAutomaticContentRefreshEnabled;
    private DateTimeOffset _nextAutomaticContentCheck;
    private int _automaticRefreshFailures;
    private readonly object _progressLock = new();
    private CanvaLoadProgress? _pendingProgress;
    private bool _progressQueued;
    private bool _isAutomaticRefreshRunning;

    public MainViewModel(IOutputCoordinator coordinator, OutputWindowManager outputWindowManager, DisplayService displayService, DisplayIdentificationService displayIdentificationService, DiagnosticsExportService diagnosticsExportService, JsonPreferencesStore preferencesStore, PresentationCacheStore presentationCacheStore, CanvaConnectService canva, ICanvaPresenterTracker canvaPresenterAccessibilityTracker, AppPreferences preferences)
    {
        _coordinator = coordinator;
        _outputWindowManager = outputWindowManager;
        _displayService = displayService;
        _displayIdentificationService = displayIdentificationService;
        _diagnosticsExportService = diagnosticsExportService;
        _preferencesStore = preferencesStore;
        _presentationCacheStore = presentationCacheStore;
        _canva = canva;
        _canvaPresenterAccessibilityTracker = canvaPresenterAccessibilityTracker;
        _snapshot = coordinator.Snapshot;
        _isOperatorTopmost = preferences.IsOperatorTopmost;
        _canvaClientId = canva.ConfiguredClientId ?? string.Empty;
        _savedCanvaPresentationId = preferences.SelectedPresentationId;
        _savedCanvaPresentationTitle = preferences.SelectedPresentationTitle;
        _lastCanvaContentRefresh = preferences.SelectedPresentationRefreshedAt;
        _isPresenterSyncEnabled = preferences.IsPresenterSyncEnabled;
        _isAutomaticContentRefreshEnabled = preferences.IsAutomaticContentRefreshEnabled;
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

        Displays = new ObservableCollection<DisplayOption>(_displayService.GetDisplays());
        CanvaPresentations = [];
        FontFamilies = ["Aptos", "Segoe UI", "Arial", "Verdana"];
        FontSizes = [32d, 40d, 48d, 56d, 64d, 72d, 88d, 104d, 120d, 144d, 160d];
        NextLineFontSizes = [16d, 18d, 20d, 24d, 28d, 30d, 32d, 36d, 40d, 48d, 56d, 64d];

        PreviewCommand = new RelayCommand(ShowPreview);
        StartOutputCommand = new RelayCommand(StartOutput, CanStartOutput);
        StopOutputCommand = new RelayCommand(StopOutput, () => Snapshot.IsLive);
        BlankOutputCommand = new RelayCommand(() => _coordinator.SetBlanked(!Snapshot.IsBlanked), () => Snapshot.IsLive);
        SelectMirrorCommand = new RelayCommand(() => OutputMode = OutputMode.Mirror);
        SelectMonitorCommand = new RelayCommand(() => OutputMode = OutputMode.Monitor);
        PreviousPageCommand = new RelayCommand(() => { PauseForManualNavigation(); _coordinator.GoPrevious(); }, () => Snapshot.CanGoPrevious);
        NextPageCommand = new RelayCommand(() => { PauseForManualNavigation(); _coordinator.GoNext(); }, () => Snapshot.CanGoNext);
        TogglePresenterSyncCommand = new RelayCommand(TogglePresenterSync);
        RefreshDisplaysCommand = new RelayCommand(RefreshDisplays);
        IdentifyDisplaysCommand = new RelayCommand(_displayIdentificationService.Show);
        TestSetupCommand = new RelayCommand(ShowSetupDiagnostics);
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
        SelectedDisplay = Displays.FirstOrDefault(display => display.MatchesId(preferences.SelectedDisplayId)) ?? Displays.FirstOrDefault();
        _coordinator.SnapshotChanged += OnSnapshotChanged;
        _canva.LoadProgressChanged += OnCanvaLoadProgressChanged;
        _canvaPresenterAccessibilityTracker.TrackingUpdated += OnCanvaPresenterTrackingUpdated;
        ApplyPresenterSyncPreference();
        _presenterHealthTimer.Tick += OnPresenterHealthTick;
        _presenterHealthTimer.Start();
        _contentRefreshTimer.Tick += OnAutomaticContentRefreshTick;
        _contentRefreshTimer.Start();
    }

    public ObservableCollection<DisplayOption> Displays { get; }
    public ObservableCollection<CanvaPresentation> CanvaPresentations { get; }
    public IReadOnlyList<string> FontFamilies { get; }
    public IReadOnlyList<double> FontSizes { get; }
    public IReadOnlyList<double> NextLineFontSizes { get; }
    public ICommand PreviewCommand { get; }
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
            OnPropertyChanged(nameof(IsPresentationSelected));
            OnPropertyChanged(nameof(IsDisplaysSelected));
            OnPropertyChanged(nameof(IsSetupDiagnosticsSelected));
            OnPropertyChanged(nameof(OperatorPageTitle));
            OnPropertyChanged(nameof(OperatorPageSubtitle));
        }
    }

    public bool IsLiveControlSelected { get => ActiveSection == OperatorSection.LiveControl; set { if (value) ActiveSection = OperatorSection.LiveControl; } }
    public bool IsPresentationSelected { get => ActiveSection == OperatorSection.Presentation; set { if (value) ActiveSection = OperatorSection.Presentation; } }
    public bool IsDisplaysSelected { get => ActiveSection == OperatorSection.Displays; set { if (value) ActiveSection = OperatorSection.Displays; } }
    public bool IsSetupDiagnosticsSelected { get => ActiveSection == OperatorSection.SetupDiagnostics; set { if (value) ActiveSection = OperatorSection.SetupDiagnostics; } }
    public string OperatorPageTitle => ActiveSection switch
    {
        OperatorSection.Presentation => "Presentation",
        OperatorSection.Displays => "Displays",
        OperatorSection.SetupDiagnostics => "Setup & diagnostics",
        _ => "Live control"
    };
    public string OperatorPageSubtitle => ActiveSection switch
    {
        OperatorSection.Presentation => "Connect Canva, refresh the available presentations, and load Sunday’s content.",
        OperatorSection.Displays => "Choose where the confidence output appears and test it safely.",
        OperatorSection.SetupDiagnostics => "Review each readiness check before going live.",
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
            OnPropertyChanged(nameof(ShowPreviewNextLine));
            OnPropertyChanged(nameof(ContrastMessage));
            OnPropertyChanged(nameof(PreviewBackgroundBrush));
            OnPropertyChanged(nameof(PreviewTextBrush));
            OnPropertyChanged(nameof(PreviewAccentBrush));
            OnPropertyChanged(nameof(PreviewNextLineBrush));
            OnPropertyChanged(nameof(PreviewNextLineFontSize));
            OnPropertyChanged(nameof(PreviewNextLineFontWeight));
            OnPropertyChanged(nameof(PreviewMirrorBackgroundBrush));
            OnPropertyChanged(nameof(PreviewMirrorAccentBrush));
            OnPropertyChanged(nameof(PreviewImagePath));
            OnPropertyChanged(nameof(HasPreviewImage));
            OnPropertyChanged(nameof(ShowMonitorPreviewImage));
            OnPropertyChanged(nameof(SelectedPage));
            OnPropertyChanged(nameof(SelectedPageTitle));
            OnPropertyChanged(nameof(PreviewPrimaryText));
            OnPropertyChanged(nameof(PreviewNextLine));
            OnPropertyChanged(nameof(CurrentPageText));
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
    public string CurrentPageText => $"Page {CurrentPageNumber} of {PageCount}";
    public bool IsLive => Snapshot.IsLive;
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
            string text = string.Join(Environment.NewLine, page.TextBlocks.Where(static item => !string.IsNullOrWhiteSpace(item)));
            return string.IsNullOrWhiteSpace(text) ? "This page has no readable text. Monitor mode will use its Canva page image." : text;
        }
    }
    public string PreviewNextLine => Snapshot.NextLine ?? string.Empty;
    public bool ShowPreviewNextLine => Snapshot.Layout is not MonitorLayout.CurrentOnly and not MonitorLayout.SpeakerFocused && !string.IsNullOrWhiteSpace(Snapshot.NextLine);
    public string? PreviewImagePath => SelectedPage?.FallbackImagePath;
    public bool HasPreviewImage => !string.IsNullOrWhiteSpace(PreviewImagePath);
    public bool ShowMonitorPreviewImage => HasPreviewImage && SelectedPage is { HasFormattedText: false };
    public Brush PreviewMirrorBackgroundBrush => BrushFrom(SelectedPage?.MirrorBackground ?? "#0D0E10", Brushes.Black);
    public Brush PreviewMirrorAccentBrush => BrushFrom(SelectedPage?.MirrorAccent ?? "#75E6C8", Brushes.MediumPurple);
    public bool IsMirrorSelected { get => OutputMode == OutputMode.Mirror; set { if (value) OutputMode = OutputMode.Mirror; } }
    public bool IsMonitorSelected { get => OutputMode == OutputMode.Monitor; set { if (value) OutputMode = OutputMode.Monitor; } }

    public DisplayOption? SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (!SetProperty(ref _selectedDisplay, value)) return;
            Persist();
            StartOutputCommand.RaiseCanExecuteChanged();
            NotifyReadinessChanged();
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

    public bool IsOperatorTopmost { get => _isOperatorTopmost; set { if (SetProperty(ref _isOperatorTopmost, value)) Persist(); } }
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
    public Brush PreviewBackgroundBrush => BrushFrom(BackgroundColor, Brushes.Black);
    public Brush PreviewTextBrush => BrushFrom(TextColor, Brushes.White);
    public Brush PreviewAccentBrush => BrushFrom(AccentColor, Brushes.MediumPurple);
    public Brush PreviewNextLineBrush => BrushFrom(NextLineColor, Brushes.White);
    public double PreviewNextLineFontSize => Math.Clamp(NextLineFontSize * 0.55d, 10d, 32d);
    public System.Windows.FontWeight PreviewNextLineFontWeight => IsNextLineBold ? FontWeights.Bold : FontWeights.Normal;
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
        private set => SetProperty(ref _isCanvaDownloadProgressVisible, value);
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
            _nextAutomaticContentCheck = DateTimeOffset.MinValue;
            OnPropertyChanged(nameof(PresentationFreshnessText));
            Persist();
        }
    }
    public string PresentationFreshnessText => IsPresentationUpdateAvailable
        ? IsAutomaticContentRefreshEnabled ? "A newer version is available. CanvaStage will retry the content refresh automatically."
            : "A newer version is available in Canva. Select Re-download to update the local copy."
        : Snapshot.Document.Id == "demo-sunday"
            ? "Load a Canva presentation to check for updates."
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
    public string CanvaDiagnosticDetail => _canva.IsConnected ? "The shared Windows operator account has valid local Canva authorization." : "Open Presentation to configure or reconnect the church Canva integration.";
    public string PresentationDiagnostic => Snapshot.Document.Id == "demo-sunday" ? "WARNING · Demo deck loaded" : "PASS · Canva presentation loaded";
    public string PresentationDiagnosticDetail => Snapshot.Document.Id == "demo-sunday" ? "Select and Apply a Canva presentation before using live content." : $"{Snapshot.Document.Title} is available for manual page selection.";
    public string DisplayDiagnostic => Displays.Count > 1 && SelectedDisplay is not null ? "PASS · Confidence display available" : "WARNING · Preview-only setup";
    public string DisplayDiagnosticDetail => Displays.Count > 1 ? $"{SelectedDisplay?.Label ?? "Choose a display"} is available for output." : "Windows currently reports one display. Preview is safe; extend displays for independent output.";
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
            ((RelayCommand)CleanOldCacheCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ClearCacheCommand).RaiseCanExecuteChanged();
            ((RelayCommand)UseDemoDeckCommand).RaiseCanExecuteChanged();
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
        _lifetimeCancellation.Cancel();
        _contentRefreshTimer.Stop();
        _contentRefreshTimer.Tick -= OnAutomaticContentRefreshTick;
        _presenterHealthTimer.Stop();
        _presenterHealthTimer.Tick -= OnPresenterHealthTick;
        _coordinator.SnapshotChanged -= OnSnapshotChanged;
        _canva.LoadProgressChanged -= OnCanvaLoadProgressChanged;
        _canvaPresenterAccessibilityTracker.TrackingUpdated -= OnCanvaPresenterTrackingUpdated;
        _canvaPresenterAccessibilityTracker.Dispose();
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
    private void ShowPreview() => _outputWindowManager.ShowPreview();
    private bool CanStartOutput() => SelectedDisplay is not null;

    private void StartOutput()
    {
        if (!_outputWindowManager.ShowOnDisplay(SelectedDisplay?.Id))
            System.Windows.MessageBox.Show("That display is no longer available. Refresh the display list and choose another target.", "Display unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void StopOutput() => _outputWindowManager.StopOutput();

    private void ApplyPresenterSyncPreference()
    {
        _isPresenterSyncPaused = false;
        _lastPresenterUpdate = null;
        if (IsPresenterSyncEnabled)
        {
            _canvaPresenterAccessibilityTracker.Start();
            SetPresenterState(PresenterSyncState.Waiting, "Waiting for Canva Presenter View…");
        }
        else
        {
            _canvaPresenterAccessibilityTracker.Stop();
            SetPresenterState(PresenterSyncState.Manual, "Automatic Presenter sync is off. Use manual page controls or Resume sync.");
        }

        OnPropertyChanged(nameof(IsCanvaPresenterTracking));
    }
    private void RefreshDisplays()
    {
        string? selectedId = SelectedDisplay?.Id;
        Displays.Clear();
        foreach (DisplayOption display in _displayService.GetDisplays()) Displays.Add(display);
        SelectedDisplay = Displays.FirstOrDefault(display => display.MatchesId(selectedId)) ?? Displays.FirstOrDefault();
        NotifyReadinessChanged();
    }

    private void ShowSetupDiagnostics()
    {
        RefreshDisplays();
        ActiveSection = OperatorSection.SetupDiagnostics;
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
        await RunCanvaActionAsync(() => LoadAndApplyCanvaCoreAsync(presentation), message => CanvaPresentationStatus = message);
    }

    private async Task RedownloadCanvaAsync()
    {
        PresentationDocument current = Snapshot.Document;
        if (current.Id == "demo-sunday") return;
        await RunCanvaActionAsync(() => LoadAndApplyCanvaCoreAsync(
            new CanvaPresentation(current.Id, current.Title, current.Pages.Count), current), message => CanvaPresentationStatus = message);
    }

    private async Task LoadAndApplyCanvaCoreAsync(CanvaPresentation presentation, PresentationDocument? expectedDocument = null)
    {
        IsCanvaDownloadProgressVisible = true;
        _canvaDownloadProgress = null;
        CanvaDownloadDescription = "Preparing Canva presentation…";
        OnPropertyChanged(nameof(IsCanvaDownloadProgressIndeterminate));
        OnPropertyChanged(nameof(CanvaDownloadProgressPercent));
        try
        {
            CanvaPresentationStatus = $"Preparing and reading {presentation.Title}. Large presentations can take up to five minutes.";
            PresentationDocument document = await Task.Run(() => _canva.LoadAsync(presentation.Id, _lifetimeCancellation.Token), _lifetimeCancellation.Token);
            if (_isClosing || expectedDocument is not null && (!ReferenceEquals(Snapshot.Document, expectedDocument) || !IsAutomaticContentRefreshEnabled && _isAutomaticRefreshRunning)) return;
            await Task.Run(() => _presentationCacheStore.Save(document), _lifetimeCancellation.Token);
            if (_isClosing) return;
            int? presenterPage = IsPresenterSyncEnabled && !_isPresenterSyncPaused
                && _lastPresenterUpdate is { State: CanvaPresenterTrackingState.PageDetected, CurrentPage: int page, TotalPages: int total }
                && total == document.Pages.Count && DateTimeOffset.UtcNow - _lastPresenterUpdate.ObservedAt <= TimeSpan.FromSeconds(3)
                ? page : null;
            _coordinator.SetDocument(document, presenterPage);
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
        await RunCanvaActionAsync(async () =>
        {
            await RefreshCanvaCoreAsync();
            CanvaConnectionStatus = "Connected using saved authorization. Presentations loaded automatically.";
            if (IsAutomaticContentRefreshEnabled && Snapshot.Document.Id != "demo-sunday")
            {
                _isAutomaticRefreshRunning = true;
                try { await CheckPresentationUpdatesCoreAsync(true); }
                finally { _isAutomaticRefreshRunning = false; }
            }
        }, message => { CanvaConnectionStatus = message; CanvaPresentationStatus = message; });
        _nextAutomaticContentCheck = DateTimeOffset.UtcNow.AddSeconds(30);
    }

    private async Task CheckPresentationUpdatesAsync() => await RunCanvaActionAsync(
        () => CheckPresentationUpdatesCoreAsync(IsAutomaticContentRefreshEnabled), message => CanvaPresentationStatus = message);

    private async Task CheckPresentationUpdatesCoreAsync(bool applyUpdates)
    {
        PresentationDocument applied = Snapshot.Document;
        if (applied.Id == "demo-sunday") return;
        CanvaPresentation current = await _canva.GetPresentationAsync(applied.Id, _lifetimeCancellation.Token);
        if (_isClosing || !ReferenceEquals(Snapshot.Document, applied)) return;
        CanvaPresentation? listed = CanvaPresentations.FirstOrDefault(item => item.Id == current.Id);
        if (listed is not null)
        {
            int index = CanvaPresentations.IndexOf(listed);
            CanvaPresentations[index] = current;
            if (SelectedCanvaPresentation == listed) SelectedCanvaPresentation = current;
        }
        EvaluatePresentationFreshness(current);
        if (applyUpdates && (IsPresentationUpdateAvailable || applied.SourceModifiedAt is null && current.ModifiedAt is not null))
        {
            await LoadAndApplyCanvaCoreAsync(current, applied);
            return;
        }
        CanvaPresentationStatus = IsPresentationUpdateAvailable
            ? "Canva has a newer version. Select Re-download to update the cached presentation."
            : _hasPresentationFreshnessCheck ? "The downloaded presentation is up to date."
                : "Canva did not provide comparable edit timestamps. Use Re-download to refresh the presentation.";
    }

    private async void OnAutomaticContentRefreshTick(object? sender, EventArgs eventArgs)
        => await CheckAutomaticContentRefreshAsync();

    internal async Task CheckAutomaticContentRefreshAsync(bool force = false)
    {
        if (_isClosing || !IsAutomaticContentRefreshEnabled || !_canva.IsConnected
            || IsCanvaBusy || Snapshot.Document.Id == "demo-sunday" || !force && DateTimeOffset.UtcNow < _nextAutomaticContentCheck) return;
        _isAutomaticRefreshRunning = true;
        try
        {
            bool succeeded = await RunCanvaActionAsync(() => CheckPresentationUpdatesCoreAsync(true),
                message => CanvaPresentationStatus = $"Automatic content refresh: {message} The current presentation remains available.");
            _automaticRefreshFailures = succeeded ? 0 : Math.Min(4, _automaticRefreshFailures + 1);
            _nextAutomaticContentCheck = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 30 * Math.Pow(2, _automaticRefreshFailures)));
        }
        finally { _isAutomaticRefreshRunning = false; }
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
                Displays.Select(display => display.Label).ToArray()));
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
        catch (Exception exception) { PrivacySafeLog.Write($"Canva action failed unexpectedly ({exception.GetType().Name})."); setFailureStatus("CanvaStage could not finish this Canva action. Try again; if it repeats, export diagnostics from Setup & diagnostics."); }
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
            SetPresenterState(PresenterSyncState.Waiting, "Waiting for a fresh Presenter update…");
            return;
        }
        if (update.State != CanvaPresenterTrackingState.PageDetected)
        {
            SetPresenterState(update.State == CanvaPresenterTrackingState.Error ? PresenterSyncState.Recovering : PresenterSyncState.Waiting, update.Message);
            return;
        }
        if (Snapshot.Document.Id == "demo-sunday")
        {
            SetPresenterState(PresenterSyncState.Waiting, "Load a Canva presentation to enable Presenter sync. The demo uses manual controls.");
            return;
        }
        if (update.CurrentPage is null || update.TotalPages is null)
        {
            SetPresenterState(PresenterSyncState.Waiting, "Waiting for a complete Presenter page counter…");
            return;
        }
        int pageCount = Snapshot.Document.Pages.Count;
        if (update.TotalPages.Value != pageCount)
        {
            SetPresenterState(PresenterSyncState.Mismatch,
                $"Canva Presenter reports {update.TotalPages.Value} visible pages; CanvaStage has {pageCount}. Refresh and apply the matching presentation.");
            return;
        }
        if (Snapshot.Document.FindPage(update.CurrentPage.Value) is null)
        {
            SetPresenterState(PresenterSyncState.Mismatch, "The Presenter page is outside the applied presentation. Waiting for a valid page.");
            return;
        }
        if (Snapshot.SelectedPageNumber != update.CurrentPage.Value)
            _coordinator.SelectPage(update.CurrentPage.Value);
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
        OnPropertyChanged(nameof(PresenterScanHealth));
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
        Snapshot = snapshot;
        if (documentChanged)
        {
            ReevaluatePresenterSync();
            EvaluatePresentationFreshness();
            OnPropertyChanged(nameof(CacheSummary));
        }
        Persist();
    }

    private void NotifyReadinessChanged()
    {
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
    }

    private void Persist()
    {
        if (_isSaving) return;
        _isSaving = true;
        try
        {
            _preferencesStore.Save(new AppPreferences(SelectedDisplay?.Id, CurrentPageNumber, Snapshot.Mode, Snapshot.Layout, Snapshot.NextLineOption, Snapshot.Appearance, IsOperatorTopmost, SelectedCanvaPresentation?.Id ?? _savedCanvaPresentationId, SelectedCanvaPresentation?.Title ?? _savedCanvaPresentationTitle, _lastCanvaContentRefresh, IsPresenterSyncEnabled, IsAutomaticContentRefreshEnabled));
        }
        finally { _isSaving = false; }
    }
}
