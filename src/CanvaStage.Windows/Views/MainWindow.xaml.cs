using CanvaStage.Windows.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace CanvaStage.Windows.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
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
        base.OnClosing(eventArgs);
    }

    private void PickBackgroundColor(object sender, RoutedEventArgs eventArgs) => PickColor(_viewModel.BackgroundColor, color => _viewModel.BackgroundColor = color);

    private void PickTextColor(object sender, RoutedEventArgs eventArgs) => PickColor(_viewModel.TextColor, color => _viewModel.TextColor = color);

    private void PickAccentColor(object sender, RoutedEventArgs eventArgs) => PickColor(_viewModel.AccentColor, color => _viewModel.AccentColor = color);

    private void CanvaSecretChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is System.Windows.Controls.PasswordBox passwordBox)
        {
            _viewModel.SetCanvaClientSecret(passwordBox.Password);
        }
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
}
