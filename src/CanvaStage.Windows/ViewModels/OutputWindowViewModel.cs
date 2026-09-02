using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using System.Globalization;
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
    private double _fittedFontSize;

    public OutputWindowViewModel(IOutputCoordinator coordinator)
    {
        _coordinator = coordinator;
        _snapshot = coordinator.Snapshot;
        _fallbackImage = CreateFallbackImage(_snapshot.CurrentPage?.FallbackImagePath);
        _fittedFontSize = EffectiveFontSize;
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
            _fittedFontSize = EffectiveFontSize;
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
            OnPropertyChanged(nameof(FittedFontSize));
            OnPropertyChanged(nameof(FallbackImage));
            OnPropertyChanged(nameof(HasFallbackImage));
            OnPropertyChanged(nameof(ShowFallbackImage));
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
    public double FittedFontSize => _fittedFontSize;
    public string CurrentTime { get => _currentTime; set => SetProperty(ref _currentTime, value); }
    public Brush MirrorBackground => BrushFrom(Page?.MirrorBackground ?? "#0D0E10");
    public Brush MirrorAccent => BrushFrom(Page?.MirrorAccent ?? "#75E6C8");
    public Brush BackgroundBrush => BrushFrom(Snapshot.Appearance.BackgroundColor);
    public Brush TextBrush => BrushFrom(Snapshot.Appearance.TextColor);
    public Brush AccentBrush => BrushFrom(Snapshot.Appearance.AccentColor);
    public BitmapImage? FallbackImage => _fallbackImage;
    public bool HasFallbackImage => _fallbackImage is not null;
    public bool ShowFallbackImage => IsMonitor && HasFallbackImage && Page is { HasFormattedText: false };

    public void UpdateClock() => CurrentTime = DateTime.Now.ToString("h:mm tt");

    public void FitPrimaryText(double availableWidth, double availableHeight, double pixelsPerDip)
    {
        if (availableWidth <= 0d || availableHeight <= 0d || string.IsNullOrWhiteSpace(PrimaryText))
        {
            return;
        }

        double minimumSize = 12d;
        double low = minimumSize;
        double high = EffectiveFontSize;
        double best = minimumSize;
        for (int iteration = 0; iteration < 12; iteration++)
        {
            double candidate = (low + high) / 2d;
            if (FitsPrimaryText(candidate, availableWidth, availableHeight, pixelsPerDip))
            {
                best = candidate;
                low = candidate;
            }
            else
            {
                high = candidate;
            }
        }

        if (Math.Abs(_fittedFontSize - best) < 0.25d)
        {
            return;
        }

        _fittedFontSize = best;
        OnPropertyChanged(nameof(FittedFontSize));
    }

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

    private bool FitsPrimaryText(double fontSize, double availableWidth, double availableHeight, double pixelsPerDip)
    {
        var typeface = new System.Windows.Media.Typeface(
            new System.Windows.Media.FontFamily(Snapshot.Appearance.FontFamily),
            System.Windows.FontStyles.Normal,
            System.Windows.FontWeights.SemiBold,
            System.Windows.FontStretches.Normal);
        var formattedText = new System.Windows.Media.FormattedText(
            PrimaryText,
            CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            fontSize,
            TextBrush,
            pixelsPerDip)
        {
            MaxTextWidth = availableWidth,
            TextAlignment = System.Windows.TextAlignment.Center
        };
        return formattedText.Height <= availableHeight && formattedText.Width <= availableWidth;
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
