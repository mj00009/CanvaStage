using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using System.IO;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using BrushConverter = System.Windows.Media.BrushConverter;
using BitmapImage = System.Windows.Media.Imaging.BitmapImage;

namespace CanvaStage.Windows.ViewModels;

public sealed class OutputWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IOutputCoordinator _coordinator;
    private OutputSnapshot _snapshot;
    private string _currentTime = DateTime.Now.ToString("h:mm tt");
    private BitmapImage? _fallbackImage;

    public OutputWindowViewModel(IOutputCoordinator coordinator)
    {
        _coordinator = coordinator;
        _snapshot = coordinator.Snapshot;
        _fallbackImage = CreateFallbackImage(_snapshot.CurrentPage?.FallbackImagePath);
        coordinator.SnapshotChanged += OnSnapshotChanged;
    }

    public OutputSnapshot Snapshot
    {
        get => _snapshot;
        private set
        {
            if (!SetProperty(ref _snapshot, value))
            {
                return;
            }

            _fallbackImage = CreateFallbackImage(value.CurrentPage?.FallbackImagePath);
            OnPropertyChanged(nameof(IsBlank));
            OnPropertyChanged(nameof(IsMirror));
            OnPropertyChanged(nameof(IsMonitor));
            OnPropertyChanged(nameof(Page));
            OnPropertyChanged(nameof(PrimaryText));
            OnPropertyChanged(nameof(NextLine));
            OnPropertyChanged(nameof(MirrorBackground));
            OnPropertyChanged(nameof(MirrorAccent));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(TextBrush));
            OnPropertyChanged(nameof(AccentBrush));
            OnPropertyChanged(nameof(ShowNextLine));
            OnPropertyChanged(nameof(ShowClock));
            OnPropertyChanged(nameof(EffectiveFontSize));
            OnPropertyChanged(nameof(FallbackImage));
            OnPropertyChanged(nameof(HasFallbackImage));
        }
    }

    public bool IsBlank => !Snapshot.IsLive || Snapshot.CurrentPage is null;
    public bool IsMirror => !IsBlank && Snapshot.Mode == OutputMode.Mirror;
    public bool IsMonitor => !IsBlank && Snapshot.Mode == OutputMode.Monitor;
    public PresentationPage? Page => Snapshot.CurrentPage;
    public string PrimaryText => Page is null
        ? string.Empty
        : string.Join(Environment.NewLine, Page.TextBlocks.Where(static text => !string.IsNullOrWhiteSpace(text)));
    public string? NextLine => Snapshot.NextLine;
    public bool ShowNextLine => IsMonitor && Snapshot.Layout is not MonitorLayout.CurrentOnly and not MonitorLayout.SpeakerFocused && !string.IsNullOrWhiteSpace(NextLine);
    public bool ShowClock => IsMonitor && Snapshot.Appearance.ShowClock;
    public double EffectiveFontSize => Snapshot.Layout switch
    {
        MonitorLayout.SpeakerFocused => Math.Min(160, Snapshot.Appearance.FontSize * 1.2),
        MonitorLayout.CurrentAndNextCompact => Math.Max(32, Snapshot.Appearance.FontSize * 0.8),
        _ => Snapshot.Appearance.FontSize
    };
    public string CurrentTime { get => _currentTime; set => SetProperty(ref _currentTime, value); }
    public Brush MirrorBackground => BrushFrom(Page?.MirrorBackground ?? "#0D0E10");
    public Brush MirrorAccent => BrushFrom(Page?.MirrorAccent ?? "#75E6C8");
    public Brush BackgroundBrush => BrushFrom(Snapshot.Appearance.BackgroundColor);
    public Brush TextBrush => BrushFrom(Snapshot.Appearance.TextColor);
    public Brush AccentBrush => BrushFrom(Snapshot.Appearance.AccentColor);
    public BitmapImage? FallbackImage => _fallbackImage;
    public bool HasFallbackImage => _fallbackImage is not null;

    public void UpdateClock() => CurrentTime = DateTime.Now.ToString("h:mm tt");

    public void Dispose() => _coordinator.SnapshotChanged -= OnSnapshotChanged;

    private static Brush BrushFrom(string value)
    {
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(value)!;
        }
        catch (FormatException)
        {
            return Brushes.Black;
        }
    }

    private static BitmapImage? CreateFallbackImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path!, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void OnSnapshotChanged(object? sender, OutputSnapshot snapshot) => Snapshot = snapshot;
}
