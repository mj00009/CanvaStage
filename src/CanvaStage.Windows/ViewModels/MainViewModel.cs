using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Core.Utilities;
using CanvaStage.Windows.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace CanvaStage.Windows.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IOutputCoordinator _coordinator;
    private readonly OutputWindowManager _outputWindowManager;
    private readonly DisplayService _displayService;
    private readonly JsonPreferencesStore _preferencesStore;
    private OutputSnapshot _snapshot;
    private DisplayOption? _selectedDisplay;
    private bool _isOperatorTopmost;
    private bool _isSaving;

    public MainViewModel(
        IOutputCoordinator coordinator,
        OutputWindowManager outputWindowManager,
        DisplayService displayService,
        JsonPreferencesStore preferencesStore,
        AppPreferences preferences)
    {
        _coordinator = coordinator;
        _outputWindowManager = outputWindowManager;
        _displayService = displayService;
        _preferencesStore = preferencesStore;
        _snapshot = coordinator.Snapshot;
        _isOperatorTopmost = preferences.IsOperatorTopmost;

        Displays = new ObservableCollection<DisplayOption>(_displayService.GetDisplays());
        FontFamilies = ["Aptos", "Segoe UI", "Arial", "Verdana"];
        FontSizes = [32d, 40d, 48d, 56d, 64d, 72d, 88d, 104d, 120d, 144d, 160d];

        PreviewCommand = new RelayCommand(ShowPreview);
        StartOutputCommand = new RelayCommand(StartOutput, CanStartOutput);
        StopOutputCommand = new RelayCommand(StopOutput, () => Snapshot.IsLive);
        PreviousPageCommand = new RelayCommand(_coordinator.GoPrevious, () => Snapshot.CanGoPrevious);
        NextPageCommand = new RelayCommand(_coordinator.GoNext, () => Snapshot.CanGoNext);
        RefreshDisplaysCommand = new RelayCommand(RefreshDisplays);
        TestSetupCommand = new RelayCommand(ShowSetupDiagnostics);

        SelectedDisplay = Displays.FirstOrDefault(display => display.Id == preferences.SelectedDisplayId) ?? Displays.FirstOrDefault();

        _coordinator.SnapshotChanged += OnSnapshotChanged;
    }

    public ObservableCollection<DisplayOption> Displays { get; }
    public IReadOnlyList<string> FontFamilies { get; }
    public IReadOnlyList<double> FontSizes { get; }
    public ICommand PreviewCommand { get; }
    public RelayCommand StartOutputCommand { get; }
    public RelayCommand StopOutputCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public ICommand RefreshDisplaysCommand { get; }
    public ICommand TestSetupCommand { get; }

    public OutputSnapshot Snapshot
    {
        get => _snapshot;
        private set
        {
            if (!SetProperty(ref _snapshot, value))
            {
                return;
            }

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

    public int CurrentPageNumber
    {
        get => Snapshot.SelectedPageNumber;
        set => _coordinator.SelectPage(value);
    }

    public int PageCount => Snapshot.Document.Pages.Count;
    public string PageLabel => $"Page {CurrentPageNumber} of {PageCount}";
    public bool IsLive => Snapshot.IsLive;
    public string LiveStatus => IsLive ? $"LIVE  •  {Snapshot.Document.Title}  •  {PageLabel}" : "OUTPUT BLANK  •  Choose Preview or Start Output";

    public DisplayOption? SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (SetProperty(ref _selectedDisplay, value))
            {
                Persist();
                StartOutputCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsOperatorTopmost
    {
        get => _isOperatorTopmost;
        set
        {
            if (SetProperty(ref _isOperatorTopmost, value))
            {
                Persist();
            }
        }
    }

    public OutputMode OutputMode
    {
        get => Snapshot.Mode;
        set => _coordinator.SetOutputMode(value);
    }

    public MonitorLayout Layout
    {
        get => Snapshot.Layout;
        set => _coordinator.SetLayout(value);
    }

    public NextLineOption NextLineOption
    {
        get => Snapshot.NextLineOption;
        set => _coordinator.SetNextLineOption(value);
    }

    public string BackgroundColor
    {
        get => Snapshot.Appearance.BackgroundColor;
        set => SetAppearance(Snapshot.Appearance with { BackgroundColor = value });
    }

    public string TextColor
    {
        get => Snapshot.Appearance.TextColor;
        set => SetAppearance(Snapshot.Appearance with { TextColor = value });
    }

    public string AccentColor
    {
        get => Snapshot.Appearance.AccentColor;
        set => SetAppearance(Snapshot.Appearance with { AccentColor = value });
    }

    public string FontFamily
    {
        get => Snapshot.Appearance.FontFamily;
        set => SetAppearance(Snapshot.Appearance with { FontFamily = value });
    }

    public double FontSize
    {
        get => Snapshot.Appearance.FontSize;
        set => SetAppearance(Snapshot.Appearance with { FontSize = Math.Clamp(value, 32, 160) });
    }

    public bool ShowClock
    {
        get => Snapshot.Appearance.ShowClock;
        set => SetAppearance(Snapshot.Appearance with { ShowClock = value });
    }

    public string ContrastMessage
    {
        get
        {
            double ratio = ContrastAnalyzer.GetContrastRatio(TextColor, BackgroundColor);
            return ratio >= 4.5
                ? $"Contrast: {ratio:0.0}:1 — recommended for readable output."
                : ratio > 0
                    ? $"Contrast warning: {ratio:0.0}:1 is below the recommended 4.5:1."
                    : "Contrast warning: use six-digit #RRGGBB colors.";
        }
    }

    public void PrepareToClose()
    {
        _coordinator.SnapshotChanged -= OnSnapshotChanged;
        Persist();
    }

    private void ShowPreview() => _outputWindowManager.ShowPreview();

    private bool CanStartOutput() => SelectedDisplay is not null;

    private void StartOutput()
    {
        if (!_outputWindowManager.ShowOnDisplay(SelectedDisplay?.Id))
        {
            System.Windows.MessageBox.Show("That display is no longer available. Refresh the display list and choose another target.", "Display unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void StopOutput() => _outputWindowManager.StopOutput();

    private void RefreshDisplays()
    {
        string? selectedId = SelectedDisplay?.Id;
        Displays.Clear();
        foreach (DisplayOption display in _displayService.GetDisplays())
        {
            Displays.Add(display);
        }

        SelectedDisplay = Displays.FirstOrDefault(display => display.Id == selectedId) ?? Displays.FirstOrDefault();
    }

    private void ShowSetupDiagnostics()
    {
        string displayStatus = Displays.Count > 1
            ? "Pass — Windows reports more than one display."
            : "Warning — only one Windows display is available. Preview works; extend displays for independent confidence output.";
        string targetStatus = SelectedDisplay is null
            ? "Fail — choose a confidence display before starting live output."
            : $"Pass — selected confidence display: {SelectedDisplay.Label}.";

        System.Windows.MessageBox.Show(
            $"Preview output: Pass — the local demo deck is ready.\n\nConfidence display: {displayStatus}\n\nSelected target: {targetStatus}\n\nCanva connection: Not configured — Phase 2 adapter is ready to be connected.",
            "Test setup",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void SetAppearance(MonitorAppearance appearance)
    {
        _coordinator.SetAppearance(appearance);
    }

    private void OnSnapshotChanged(object? sender, OutputSnapshot snapshot)
    {
        Snapshot = snapshot;
        Persist();
    }

    private void Persist()
    {
        if (_isSaving)
        {
            return;
        }

        _isSaving = true;
        try
        {
            _preferencesStore.Save(new AppPreferences(
                SelectedDisplay?.Id,
                CurrentPageNumber,
                Snapshot.Mode,
                Snapshot.Layout,
                Snapshot.NextLineOption,
                Snapshot.Appearance,
                IsOperatorTopmost));
        }
        finally
        {
            _isSaving = false;
        }
    }
}
