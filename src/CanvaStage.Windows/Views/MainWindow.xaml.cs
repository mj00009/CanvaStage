using CanvaStage.Windows.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace CanvaStage.Windows.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private WindowState _fullViewWindowState = WindowState.Maximized;
    private Rect _fullViewBounds = Rect.Empty;

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

    private void EnterCompactView(object sender, RoutedEventArgs eventArgs) => SetCompactView(true);

    private void ExitCompactView(object sender, RoutedEventArgs eventArgs) => SetCompactView(false);

    internal void SetCompactViewForAutomation(bool isCompact) => SetCompactView(isCompact);

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
            WindowState = WindowState.Normal;
            MinWidth = 320;
            MinHeight = 300;

            Rect workArea = SystemParameters.WorkArea;
            Width = Math.Clamp(workArea.Width * 0.20d, 320d, 400d);
            Height = Math.Clamp(workArea.Height * 0.25d, 300d, 340d);
            Left = Math.Max(workArea.Left, workArea.Right - Width - 16d);
            Top = workArea.Top + 16d;
            FullViewRoot.Visibility = Visibility.Collapsed;
            CompactViewRoot.Visibility = Visibility.Visible;
            return;
        }

        if (CompactViewRoot.Visibility != Visibility.Visible)
        {
            return;
        }

        CompactViewRoot.Visibility = Visibility.Collapsed;
        FullViewRoot.Visibility = Visibility.Visible;
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
        bool narrow = width < 900;
        SidebarColumn.Width = new GridLength(narrow ? 0 : width < 1250 ? 230 : 286);
        Sidebar.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        SidebarFooter.Visibility = height < 650 ? Visibility.Collapsed : Visibility.Visible;
        NarrowNavigation.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed;
        MainContent.Margin = new Thickness(width < 1250 ? 16 : 30, 20, 16, 20);
        bool stacked = width < 1250;
        LiveOptionsColumn.Width = new GridLength(stacked ? 0 : 300);
        System.Windows.Controls.Grid.SetRow(LiveOptionsPanel, stacked ? 1 : 0);
        System.Windows.Controls.Grid.SetColumn(LiveOptionsPanel, stacked ? 0 : 1);
    }
}
