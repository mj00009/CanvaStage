using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Core.Utilities;
using CanvaStage.Windows.Services;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;

namespace CanvaStage.Windows.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IOutputCoordinator _coordinator;
    private readonly OutputWindowManager _outputWindowManager;
    private readonly DisplayService _displayService;
    private readonly JsonPreferencesStore _preferencesStore;
    private readonly CanvaConnectService _canva;
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

    public MainViewModel(IOutputCoordinator coordinator, OutputWindowManager outputWindowManager, DisplayService displayService, JsonPreferencesStore preferencesStore, CanvaConnectService canva, AppPreferences preferences)
    {
        _coordinator = coordinator;
        _outputWindowManager = outputWindowManager;
        _displayService = displayService;
        _preferencesStore = preferencesStore;
        _canva = canva;
        _snapshot = coordinator.Snapshot;
        _isOperatorTopmost = preferences.IsOperatorTopmost;
        _canvaClientId = canva.ConfiguredClientId ?? string.Empty;
        _savedCanvaPresentationId = preferences.SelectedPresentationId;
        _canvaConnectionStatus = canva.IsConnected
            ? "Connected locally. Refresh presentations when you are ready."
            : canva.HasSavedConfiguration
                ? "Canva is configured for this Windows user. Select Connect to authorize it."
                : "Not configured. Add the credentials from your Canva Developer Portal integration.";
        _canvaPresentationStatus = "Refresh the connected account's presentation list, then select one and Apply.";

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

        SelectedDisplay = Displays.FirstOrDefault(display => display.Id == preferences.SelectedDisplayId) ?? Displays.FirstOrDefault();
        _coordinator.SnapshotChanged += OnSnapshotChanged;
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
    public string RedirectUrl => CanvaConnectService.RedirectUrl;

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
            StartOutputCommand.RaiseCanExecuteChanged();
            StopOutputCommand.RaiseCanExecuteChanged();
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
        }
    }

    public int CurrentPageNumber { get => Snapshot.SelectedPageNumber; set => _coordinator.SelectPage(value); }
    public int PageCount => Snapshot.Document.Pages.Count;
    public string PageLabel => $"Page {CurrentPageNumber} of {PageCount}";
    public bool IsLive => Snapshot.IsLive;
    public string LiveStatus => IsLive ? $"LIVE  •  {Snapshot.Document.Title}  •  {PageLabel}" : $"OUTPUT BLANK  •  {Snapshot.Document.Title} is ready";

    public DisplayOption? SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (!SetProperty(ref _selectedDisplay, value)) return;
            Persist();
            StartOutputCommand.RaiseCanExecuteChanged();
        }
    }

    public CanvaPresentation? SelectedCanvaPresentation
    {
        get => _selectedCanvaPresentation;
        set
        {
            if (!SetProperty(ref _selectedCanvaPresentation, value)) return;
            _savedCanvaPresentationId = value?.Id;
            Persist();
            ApplyCanvaCommand.RaiseCanExecuteChanged();
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
    public string CanvaClientId { get => _canvaClientId; set => SetProperty(ref _canvaClientId, value); }
    public string CanvaConnectionStatus { get => _canvaConnectionStatus; private set => SetProperty(ref _canvaConnectionStatus, value); }
    public string CanvaPresentationStatus { get => _canvaPresentationStatus; private set => SetProperty(ref _canvaPresentationStatus, value); }
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
    public void PrepareToClose() { _coordinator.SnapshotChanged -= OnSnapshotChanged; Persist(); }
    private void ShowPreview() => _outputWindowManager.ShowPreview();
    private bool CanStartOutput() => SelectedDisplay is not null;

    private void StartOutput()
    {
        if (!_outputWindowManager.ShowOnDisplay(SelectedDisplay?.Id))
            System.Windows.MessageBox.Show("That display is no longer available. Refresh the display list and choose another target.", "Display unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void StopOutput() => _outputWindowManager.StopOutput();
    private void RefreshDisplays()
    {
        string? selectedId = SelectedDisplay?.Id;
        Displays.Clear();
        foreach (DisplayOption display in _displayService.GetDisplays()) Displays.Add(display);
        SelectedDisplay = Displays.FirstOrDefault(display => display.Id == selectedId) ?? Displays.FirstOrDefault();
    }

    private void ShowSetupDiagnostics()
    {
        string displayStatus = Displays.Count > 1 ? "Pass — Windows reports more than one display." : "Warning — only one Windows display is available. Preview works; extend displays for independent confidence output.";
        string canvaStatus = _canva.IsConnected ? "Pass — connected for this Windows user." : _canva.HasSavedConfiguration ? "Configured — select Connect to authorize the saved integration." : "Not configured — add a Client ID and Client Secret, then connect.";
        System.Windows.MessageBox.Show($"Preview output: Pass — {Snapshot.Document.Title} is ready.\n\nConfidence display: {displayStatus}\n\nCanva connection: {canvaStatus}\n\nPrivacy: credentials and tokens are stored only in protected Windows user storage.", "Test setup", MessageBoxButton.OK, MessageBoxImage.Information);
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
        await RunCanvaActionAsync(async () =>
        {
            CanvaPresentationStatus = $"Preparing and reading {presentation.Title}. Large presentations can take up to five minutes.";
            PresentationDocument document = await _canva.LoadAsync(presentation.Id);
            _coordinator.SetDocument(document, Snapshot.SelectedPageNumber);
            CanvaPresentationStatus = $"{document.Title} is ready. Output remains blank until you open Preview or Start output.";
            Persist();
        }, message => CanvaPresentationStatus = message);
    }

    private void UseDemoDeck() { _coordinator.SetDocument(DemoPresentationProvider.Create()); CanvaPresentationStatus = "The local demo deck is ready. Canva remains connected."; Persist(); }
    private void CopyRedirectUrl() { System.Windows.Clipboard.SetText(RedirectUrl); CanvaConnectionStatus = "The redirect URL was copied. Add it under Authorized redirects in your Canva Developer Portal integration."; }

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

    private void SetAppearance(MonitorAppearance appearance) => _coordinator.SetAppearance(appearance);
    private void OnSnapshotChanged(object? sender, OutputSnapshot snapshot) { Snapshot = snapshot; Persist(); }

    private void Persist()
    {
        if (_isSaving) return;
        _isSaving = true;
        try
        {
            _preferencesStore.Save(new AppPreferences(SelectedDisplay?.Id, CurrentPageNumber, Snapshot.Mode, Snapshot.Layout, Snapshot.NextLineOption, Snapshot.Appearance, IsOperatorTopmost, SelectedCanvaPresentation?.Id, SelectedCanvaPresentation?.Title));
        }
        finally { _isSaving = false; }
    }
}
