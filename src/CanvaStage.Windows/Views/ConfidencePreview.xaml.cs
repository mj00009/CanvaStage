using UserControl = System.Windows.Controls.UserControl;
using CanvaStage.Windows.ViewModels;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace CanvaStage.Windows.Views;

public partial class ConfidencePreview : UserControl
{
    private MainViewModel? _viewModel;
    private bool _fitQueued;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(15) };

    public ConfidencePreview()
    {
        InitializeComponent();
        Loaded += (_, _) => { Attach(); _clock.Start(); QueueFit(); };
        Unloaded += (_, _) => { Detach(); _clock.Stop(); };
        DataContextChanged += (_, _) => { if (IsLoaded) { Attach(); QueueFit(); } };
        SizeChanged += (_, _) => QueueFit();
        _clock.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("h:mm tt");
    }

    private void Attach()
    {
        Detach();
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelChanged;
    }

    private void Detach()
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel = null;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs eventArgs) => QueueFit();

    private void QueueFit()
    {
        if (_fitQueued) return;
        _fitQueued = true;
        Dispatcher.BeginInvoke(() => { _fitQueued = false; FitText(); }, DispatcherPriority.Render);
    }

    internal void FitText(bool forVerification = false)
    {
        MainViewModel? viewModel = _viewModel ?? DataContext as MainViewModel;
        if (viewModel is null || (!IsVisible && !forVerification)) return;
        ClockText.Text = DateTime.Now.ToString("h:mm tt");
        UpdateLayout();
        double width = PreviewTextHost.ActualWidth;
        double height = PreviewTextHost.ActualHeight;
        if (width <= 0 || height <= 0) return;
        double low = 8;
        double high = viewModel.FontSize;
        double best = low;
        for (int index = 0; index < 12; index++)
        {
            double candidate = (low + high) / 2;
            FormattedText text = new(viewModel.PreviewPrimaryText, CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight,
                new Typeface(PreviewText.FontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                candidate, PreviewText.Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth = width };
            if (text.Height <= height && text.Width <= width) { best = candidate; low = candidate; }
            else high = candidate;
        }
        PreviewText.FontSize = best;
    }
}
