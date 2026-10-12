using CanvaStage.Windows.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace CanvaStage.Windows.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private WindowState _fullViewWindowState = WindowState.Maximized;
    private Rect _fullViewBounds = Rect.Empty;
    private double _fullViewMaxHeight = double.PositiveInfinity;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        MinWidth = Math.Min(MinWidth, SystemParameters.WorkArea.Width);
        MinHeight = Math.Min(MinHeight, SystemParameters.WorkArea.Height);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        SizeChanged += (_, _) => UpdateResponsiveLayout(ActualWidth, ActualHeight);
    }

    protected override void OnClosing(CancelEventArgs eventArgs)
    {
        if (_viewModel.IsLive)
        {
            MessageBoxResult result = System.Windows.MessageBox.Show(
                "Confidence output is live. Closing Canva Stage will blank and close the output window. Continue?",
                "Stop live output?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                eventArgs.Cancel = true;
                return;
            }
        }

        _viewModel.PrepareToClose();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosing(eventArgs);
    }

    private void PickBackgroundColor(object sender, RoutedEventArgs eventArgs) => PickColor(_viewModel.BackgroundColor, color => _viewModel.BackgroundColor = color);

    private void PickTextColor(object sender, RoutedEventArgs eventArgs) => PickColor(_viewModel.TextColor, color => _viewModel.TextColor = color);

    private void PickAccentColor(object sender, RoutedEventArgs eventArgs) => PickColor(_viewModel.AccentColor, color => _viewModel.AccentColor = color);

    private void PickNextLineColor(object sender, RoutedEventArgs eventArgs) => PickColor(_viewModel.NextLineColor, color => _viewModel.NextLineColor = color);

    private void EnterCompactView(object sender, RoutedEventArgs eventArgs) => SetCompactView(true);

    private void ExitCompactView(object sender, RoutedEventArgs eventArgs) => SetCompactView(false);

    internal void SetCompactViewForAutomation(bool isCompact) => SetCompactView(isCompact);

    internal void ScrollContentToEndForAutomation() => ContentScrollViewer.ScrollToEnd();

    internal void ScrollContentToTopForAutomation() => ContentScrollViewer.ScrollToTop();

    private void CanvaSecretChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            _viewModel.SetCanvaClientSecret(passwordBox.Password);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainViewModel.ShowSecretInput) && !_viewModel.ShowSecretInput)
            CanvaSecretInput.Clear();
    }

    private void PickColor(string initialValue, Action<string> apply)
    {
        var dialog = new ColorPickerWindow(initialValue)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            apply(dialog.SelectedColor);
        }
    }

    private void SetCompactView(bool isCompact)
    {
        if (isCompact)
        {
            if (CompactViewRoot.Visibility == Visibility.Visible)
            {
                return;
            }

            _fullViewWindowState = WindowState;
            _fullViewBounds = RestoreBounds;
            _fullViewMaxHeight = MaxHeight;
            WindowState = WindowState.Normal;
            MinWidth = 240;
            MinHeight = 240;

            Rect workArea = SystemParameters.WorkArea;
            Width = Math.Clamp(workArea.Width * 0.20d, 320d, 400d);
            MinHeight = Math.Min(240, Math.Max(1, workArea.Height - 32));
            MaxHeight = Math.Max(MinHeight, workArea.Height - 32);
            Left = Math.Max(workArea.Left, workArea.Right - Width - 16d);
            Top = workArea.Top + 16d;
            FullViewRoot.Visibility = Visibility.Collapsed;
            CompactViewRoot.Visibility = Visibility.Visible;
            _viewModel.SetCompactViewState(true);
            CompactPreviewFrame.MaxHeight = double.PositiveInfinity;
            SizeToContent = SizeToContent.Height;
            return;
        }

        if (CompactViewRoot.Visibility != Visibility.Visible)
        {
            return;
        }

        CompactViewRoot.Visibility = Visibility.Collapsed;
        FullViewRoot.Visibility = Visibility.Visible;
        _viewModel.SetCompactViewState(false);
        SizeToContent = SizeToContent.Manual;
        MaxHeight = _fullViewMaxHeight;
        MinWidth = Math.Min(600, SystemParameters.WorkArea.Width);
        MinHeight = Math.Min(420, SystemParameters.WorkArea.Height);
        if (_fullViewWindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Maximized;
            return;
        }

        if (!_fullViewBounds.IsEmpty)
        {
            Left = _fullViewBounds.Left;
            Top = _fullViewBounds.Top;
            Width = Math.Max(MinWidth, _fullViewBounds.Width);
            Height = Math.Max(MinHeight, _fullViewBounds.Height);
        }

        WindowState = _fullViewWindowState;
    }

    private void UpdateResponsiveLayout(double width, double height)
    {
        if (CompactViewRoot.Visibility == Visibility.Visible && SizeToContent == SizeToContent.Manual)
        {
            // Short windows reduce the copied preview first, so portrait sources
            // do not shrink the operator buttons to unreadable sizes.
            double widthScale = Math.Min(1, Math.Max(0.1, (width - 16) / 324));
            CompactPreviewFrame.MaxHeight = Math.Max(48, (height - 40) / widthScale - 220);
        }
        bool narrow = width < 900;
        SidebarColumn.Width = new GridLength(narrow ? 0 : width < 1250 ? 230 : 286);
        Sidebar.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        SidebarFooter.Visibility = height < 650 ? Visibility.Collapsed : Visibility.Visible;
        NarrowNavigation.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed;
        MainContent.Margin = new Thickness(width < 1250 ? 16 : 30, 20, 16, 20);
        bool stacked = width < 1250;
        LiveOptionsColumn.Width = new GridLength(stacked ? 0 : 300);
        System.Windows.Controls.Grid.SetRow(LiveOptionsPanel, stacked ? 2 : 1);
        System.Windows.Controls.Grid.SetColumn(LiveOptionsPanel, stacked ? 0 : 1);
    }
}
