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
    private readonly JsonPreferencesStore _preferencesStore;
    private readonly PresentationCacheStore _presentationCacheStore;
    private readonly CanvaConnectService _canva;
    private readonly CanvaPresenterAccessibilityTracker _canvaPresenterAccessibilityTracker;
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

    public MainViewModel(IOutputCoordinator coordinator, OutputWindowManager outputWindowManager, DisplayService displayService, JsonPreferencesStore preferencesStore, PresentationCacheStore presentationCacheStore, CanvaConnectService canva, CanvaPresenterAccessibilityTracker canvaPresenterAccessibilityTracker, AppPreferences preferences)
    {
        _coordinator = coordinator;
        _outputWindowManager = outputWindowManager;
        _displayService = displayService;
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
        _canvaConnectionStatus = canva.IsConnected
            ? "Connected locally. Refresh presentations when you are ready."
            : canva.HasSavedConfiguration
                ? "Canva is configured for this Windows user. Select Connect to authorize it."
                : "Not configured. Add the credentials from your Canva Developer Portal integration.";
        _canvaPresentationStatus = _snapshot.Document.Id == "demo-sunday"
            ? "Refresh the connected account's presentation list, then select one and Apply."
            : $"Cached {_snapshot.Document.Title} is ready for Presenter sync and manual page selection.";

        Displays = new ObservableCollection<DisplayOption>(_displayService.GetDisplays());
        CanvaPresentations = [];
        FontFamilies = ["Aptos", "Segoe UI", "Arial", "Verdana"];
        FontSizes = [32d, 40d, 48d, 56d, 64d, 72d, 88d, 104d, 120d, 144d, 160d];

        PreviewCommand = new RelayCommand(ShowPreview);
        StartOutputCommand = new RelayCommand(StartOutput, CanStartOutput);
        StopOutputCommand = new RelayCommand(StopOutput, () => Snapshot.IsLive);
        PreviousPageCommand = new RelayCommand(_coordinator.GoPrevious, () => Snapshot.CanGoPrevious);
        NextPageCommand = new RelayCommand(_coordinator.GoNext, () => Snapshot.CanGoNext);
        RefreshDisplaysCommand = new RelayCommand(RefreshDisplays);
        TestSetupCommand = new RelayCommand(ShowSetupDiagnostics);
        ConnectCanvaCommand = new AsyncRelayCommand(ConnectCanvaAsync, () => !IsCanvaBusy);
        TestCanvaCommand = new AsyncRelayCommand(TestCanvaAsync, () => !IsCanvaBusy);
        DisconnectCanvaCommand = new AsyncRelayCommand(DisconnectCanvaAsync, () => !IsCanvaBusy && _canva.HasSavedConfiguration);
        RefreshCanvaCommand = new AsyncRelayCommand(RefreshCanvaAsync, () => !IsCanvaBusy && _canva.IsConnected);
        ApplyCanvaCommand = new AsyncRelayCommand(ApplyCanvaAsync, () => !IsCanvaBusy && SelectedCanvaPresentation is not null && _canva.IsConnected);
        UseDemoDeckCommand = new RelayCommand(UseDemoDeck);
        CopyRedirectUrlCommand = new RelayCommand(CopyRedirectUrl);
        OpenCanvaDeveloperPortalCommand = new RelayCommand(OpenCanvaDeveloperPortal);
        SelectedDisplay = Displays.FirstOrDefault(display => display.Id == preferences.SelectedDisplayId) ?? Displays.FirstOrDefault();
        _coordinator.SnapshotChanged += OnSnapshotChanged;
        _canva.LoadProgressChanged += OnCanvaLoadProgressChanged;
        _canvaPresenterAccessibilityTracker.TrackingUpdated += OnCanvaPresenterTrackingUpdated;
        ApplyPresenterSyncPreference();
    }

    public ObservableCollection<DisplayOption> Displays { get; }
    public ObservableCollection<CanvaPresentation> CanvaPresentations { get; }
    public IReadOnlyList<string> FontFamilies { get; }
    public IReadOnlyList<double> FontSizes { get; }
    public ICommand PreviewCommand { get; }
    public RelayCommand StartOutputCommand { get; }
    public RelayCommand StopOutputCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public ICommand RefreshDisplaysCommand { get; }
    public ICommand TestSetupCommand { get; }
    public AsyncRelayCommand ConnectCanvaCommand { get; }
    public AsyncRelayCommand TestCanvaCommand { get; }
    public AsyncRelayCommand DisconnectCanvaCommand { get; }
    public AsyncRelayCommand RefreshCanvaCommand { get; }
    public AsyncRelayCommand ApplyCanvaCommand { get; }
    public ICommand UseDemoDeckCommand { get; }
    public ICommand CopyRedirectUrlCommand { get; }
    public ICommand OpenCanvaDeveloperPortalCommand { get; }
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

    public OperatorSection ActiveSection
    {
        get => _activeSection;
        private set
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
            OnPropertyChanged(nameof(LiveStatus));
            OnPropertyChanged(nameof(OutputMode));
            OnPropertyChanged(nameof(Layout));
            OnPropertyChanged(nameof(NextLineOption));
            OnPropertyChanged(nameof(BackgroundColor));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(AccentColor));
            OnPropertyChanged(nameof(FontFamily));
            OnPropertyChanged(nameof(FontSize));
            OnPropertyChanged(nameof(ShowClock));
            OnPropertyChanged(nameof(ContrastMessage));
            OnPropertyChanged(nameof(PreviewBackgroundBrush));
            OnPropertyChanged(nameof(PreviewTextBrush));
            OnPropertyChanged(nameof(PreviewAccentBrush));
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
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
        }
    }

    public int CurrentPageNumber { get => Snapshot.SelectedPageNumber; set => _coordinator.SelectPage(value); }
    public int PageCount => Snapshot.Document.Pages.Count;
    public string PageLabel => $"Page {CurrentPageNumber} of {PageCount}";
    public string CurrentPageText => $"Page {CurrentPageNumber} of {PageCount}";
    public bool IsLive => Snapshot.IsLive;
    public string LiveStatus => IsLive ? $"LIVE  •  {Snapshot.Document.Title}  •  {PageLabel}" : $"OUTPUT BLANK  •  {Snapshot.Document.Title} is ready";
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
    public string PreviewNextLine
    {
        get
        {
            int index = Snapshot.Document.Pages.ToList().FindIndex(page => page.Number == CurrentPageNumber);
            PresentationPage? nextPage = index >= 0 && index + 1 < Snapshot.Document.Pages.Count ? Snapshot.Document.Pages[index + 1] : null;
            return nextPage?.PrimaryText ?? "No next page";
        }
    }
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
                OnPropertyChanged(nameof(LastCanvaContentRefreshText));
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
    public bool ShowClock { get => Snapshot.Appearance.ShowClock; set => SetAppearance(Snapshot.Appearance with { ShowClock = value }); }
    public Brush PreviewBackgroundBrush => BrushFrom(BackgroundColor, Brushes.Black);
    public Brush PreviewTextBrush => BrushFrom(TextColor, Brushes.White);
    public Brush PreviewAccentBrush => BrushFrom(AccentColor, Brushes.MediumPurple);
    public string CanvaClientId { get => _canvaClientId; set => SetProperty(ref _canvaClientId, value); }
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
    public string ManualControlDiagnosticDetail => "Use Previous and Next to choose the active page. Live recognition is not enabled in this release.";
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
        }
    }

    public string ContrastMessage
    {
        get
        {
            double ratio = ContrastAnalyzer.GetContrastRatio(TextColor, BackgroundColor);
            return ratio >= 4.5 ? $"Contrast: {ratio:0.0}:1 — recommended for readable output."
                : ratio > 0 ? $"Contrast warning: {ratio:0.0}:1 is below the recommended 4.5:1."
                : "Contrast warning: use six-digit #RRGGBB colors.";
        }
    }

    public void SetCanvaClientSecret(string value) => _canvaClientSecret = value;
    public void PrepareToClose()
    {
        _coordinator.SnapshotChanged -= OnSnapshotChanged;
        _canva.LoadProgressChanged -= OnCanvaLoadProgressChanged;
        _canvaPresenterAccessibilityTracker.TrackingUpdated -= OnCanvaPresenterTrackingUpdated;
        _canvaPresenterAccessibilityTracker.Dispose();
        Persist();
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
        if (IsPresenterSyncEnabled)
        {
            _canvaPresenterAccessibilityTracker.Start();
            CanvaPresenterTrackingStatus = "Automatic Presenter sync is on. Waiting for Canva Presenter View…";
        }
        else
        {
            _canvaPresenterAccessibilityTracker.Stop();
            CanvaPresenterTrackingStatus = "Automatic Presenter sync is off. Manual page controls remain available.";
        }

        OnPropertyChanged(nameof(IsCanvaPresenterTracking));
    }
    private void RefreshDisplays()
    {
        string? selectedId = SelectedDisplay?.Id;
        Displays.Clear();
        foreach (DisplayOption display in _displayService.GetDisplays()) Displays.Add(display);
        SelectedDisplay = Displays.FirstOrDefault(display => display.Id == selectedId) ?? Displays.FirstOrDefault();
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
        CanvaConnectionStatus = "Opening Canva authorization in your browser…";
        _canva.SaveConfiguration(CanvaClientId, _canvaClientSecret);
        await _canva.ConnectAsync();
        _canvaClientSecret = string.Empty;
        CanvaConnectionStatus = "Connected. Refreshing presentations…";
        await RefreshCanvaCoreAsync();
    }, message => CanvaConnectionStatus = message);

    private async Task TestCanvaAsync() => await RunCanvaActionAsync(async () =>
    {
        _canva.SaveConfiguration(CanvaClientId, _canvaClientSecret);
        CanvaConnectionStatus = "Testing the Canva connection…";
        await _canva.TestAsync();
        _canvaClientSecret = string.Empty;
        CanvaConnectionStatus = "Connection test passed. Refresh presentations to choose one.";
    }, message => CanvaConnectionStatus = message);

    private async Task DisconnectCanvaAsync() => await RunCanvaActionAsync(async () =>
    {
        CanvaConnectionStatus = "Disconnecting Canva and removing local protected credentials…";
        await _canva.DisconnectAsync();
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
        IReadOnlyList<CanvaPresentation> presentations = await _canva.ListPresentationsAsync();
        CanvaPresentations.Clear();
        foreach (CanvaPresentation presentation in presentations) CanvaPresentations.Add(presentation);
        SelectedCanvaPresentation = CanvaPresentations.FirstOrDefault(presentation => presentation.Id == _savedCanvaPresentationId)
            ?? CanvaPresentations.FirstOrDefault(presentation => presentation.Id == Snapshot.Document.Id)
            ?? CanvaPresentations.FirstOrDefault();
        CanvaPresentationStatus = presentations.Count == 0 ? "Canva is connected, but no presentations were found for this account." : $"Found {presentations.Count} Canva presentation{(presentations.Count == 1 ? string.Empty : "s")}. Select one and Apply.";
    }

    private async Task ApplyCanvaAsync()
    {
        CanvaPresentation? presentation = SelectedCanvaPresentation;
        if (presentation is null) return;
        IsCanvaDownloadProgressVisible = true;
        _canvaDownloadProgress = null;
        CanvaDownloadDescription = "Preparing Canva presentation…";
        OnPropertyChanged(nameof(IsCanvaDownloadProgressIndeterminate));
        OnPropertyChanged(nameof(CanvaDownloadProgressPercent));
        try
        {
            await RunCanvaActionAsync(async () =>
            {
                CanvaPresentationStatus = $"Preparing and reading {presentation.Title}. Large presentations can take up to five minutes.";
                PresentationDocument document = await _canva.LoadAsync(presentation.Id);
                _coordinator.SetDocument(document, Snapshot.SelectedPageNumber);
                _presentationCacheStore.Save(document);
                _lastCanvaContentRefresh = document.RefreshedAt;
                OnPropertyChanged(nameof(LastCanvaContentRefreshText));
                CanvaPresentationStatus = $"{document.Title} is ready and cached locally. Output remains blank until you open Preview or Start output.";
                Persist();
            }, message => CanvaPresentationStatus = message);
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
        CanvaPresentationStatus = "The local demo deck is ready. Canva remains connected.";
        Persist();
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

    private async Task RunCanvaActionAsync(Func<Task> action, Action<string> setFailureStatus)
    {
        if (IsCanvaBusy) return;
        IsCanvaBusy = true;
        try { await action(); }
        catch (CanvaConnectException exception) { PrivacySafeLog.Write($"Canva action failed: {exception.Message}"); setFailureStatus(exception.Message); }
        catch (HttpRequestException) { PrivacySafeLog.Write("Canva request could not reach the service."); setFailureStatus("Canva could not be reached. Check the internet connection and try again."); }
        catch (OperationCanceledException) { PrivacySafeLog.Write("Canva action was cancelled."); setFailureStatus("The Canva action was cancelled."); }
        finally { IsCanvaBusy = false; }
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
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            ApplyCanvaLoadProgress(progress);
            return;
        }

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ApplyCanvaLoadProgress(progress));
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
        OnPropertyChanged(nameof(IsCanvaPresenterTracking));
        if (update.State != CanvaPresenterTrackingState.PageDetected
            || update.CurrentPage is null
            || update.TotalPages is null)
        {
            CanvaPresenterTrackingStatus = update.Message;
            return;
        }

        int loadedPageCount = Snapshot.Document.Pages.Count;
        if (update.TotalPages.Value != loadedPageCount)
        {
            CanvaPresenterTrackingStatus = $"Canva Presenter reports {update.TotalPages.Value} pages, but CanvaStage has {loadedPageCount}. Apply the matching presentation to enable automatic sync.";
            return;
        }

        if (Snapshot.Document.FindPage(update.CurrentPage.Value) is null)
        {
            CanvaPresenterTrackingStatus = $"Presenter page {update.CurrentPage.Value} is not available in the applied CanvaStage presentation.";
            return;
        }

        _coordinator.SelectPage(update.CurrentPage.Value);
        CanvaPresenterTrackingStatus = $"Synced to presenter page {update.CurrentPage.Value} at {update.ObservedAt.ToLocalTime():h:mm:ss tt}.";
    }

    private void SetAppearance(MonitorAppearance appearance) => _coordinator.SetAppearance(appearance);
    private void OnSnapshotChanged(object? sender, OutputSnapshot snapshot) { Snapshot = snapshot; Persist(); }

    private void NotifyReadinessChanged()
    {
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
            _preferencesStore.Save(new AppPreferences(SelectedDisplay?.Id, CurrentPageNumber, Snapshot.Mode, Snapshot.Layout, Snapshot.NextLineOption, Snapshot.Appearance, IsOperatorTopmost, SelectedCanvaPresentation?.Id ?? _savedCanvaPresentationId, SelectedCanvaPresentation?.Title ?? _savedCanvaPresentationTitle, _lastCanvaContentRefresh, IsPresenterSyncEnabled));
        }
        finally { _isSaving = false; }
    }
}
