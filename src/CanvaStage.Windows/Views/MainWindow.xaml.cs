using CanvaStage.Windows.ViewModels;
using System.ComponentModel;
using System.Drawing;
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

    private static void PickColor(string initialValue, Action<string> apply)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = TryParseColor(initialValue)
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            apply($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
        }
    }

    private static Color TryParseColor(string value)
    {
        try
        {
            return ColorTranslator.FromHtml(value);
        }
        catch (ArgumentException)
        {
            return Color.White;
        }
    }
}
