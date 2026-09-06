using CanvaStage.Core.Utilities;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace CanvaStage.Windows.Views;

public partial class ColorPickerWindow : Window
{
    private bool _isSelectingSpectrum;
    private bool _isUpdatingHex;

    public ColorPickerWindow(string initialColor)
    {
        InitializeComponent();
        SelectedColor = ColorValueConverter.TryNormalizeHex(initialColor, out string normalized) ? normalized : "#FFFFFF";
        UpdateSelection(SelectedColor, updateHexInput: true);
    }

    public string SelectedColor { get; private set; }

    private void OnHexTextChanged(object sender, TextChangedEventArgs eventArgs)
    {
        if (_isUpdatingHex)
        {
            return;
        }

        if (ColorValueConverter.TryNormalizeHex(HexInput.Text, out string normalized))
        {
            UpdateSelection(normalized, updateHexInput: false);
            ValidationMessage.Text = "Ready to apply.";
            ValidationMessage.Foreground = (Brush)FindResource("SuccessBrush");
        }
        else
        {
            ValidationMessage.Text = "Enter six hexadecimal characters, such as #75E6C8.";
            ValidationMessage.Foreground = (Brush)FindResource("DangerBrush");
        }
    }

    private void OnSwatchClick(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: string color })
        {
            UpdateSelection(color, updateHexInput: true);
        }
    }

    private void OnSpectrumMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        _isSelectingSpectrum = true;
        SpectrumSurface.CaptureMouse();
        UpdateSpectrumSelection(eventArgs.GetPosition(SpectrumSurface));
    }

    private void OnSpectrumMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (_isSelectingSpectrum && eventArgs.LeftButton == MouseButtonState.Pressed)
        {
            UpdateSpectrumSelection(eventArgs.GetPosition(SpectrumSurface));
        }
    }

    private void OnSpectrumMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (!_isSelectingSpectrum)
        {
            return;
        }

        UpdateSpectrumSelection(eventArgs.GetPosition(SpectrumSurface));
        _isSelectingSpectrum = false;
        SpectrumSurface.ReleaseMouseCapture();
    }

    private void UpdateSpectrumSelection(Point point)
    {
        if (SpectrumSurface.ActualWidth <= 0d || SpectrumSurface.ActualHeight <= 0d)
        {
            return;
        }

        double x = Math.Clamp(point.X, 0d, SpectrumSurface.ActualWidth);
        double y = Math.Clamp(point.Y, 0d, SpectrumSurface.ActualHeight);
        double hue = x / SpectrumSurface.ActualWidth * 360d;
        double brightness = 1d - (y / SpectrumSurface.ActualHeight);
        string color = ColorValueConverter.HsvToHex(hue, 1d, brightness);
        UpdateSelection(color, updateHexInput: true);

        Canvas.SetLeft(SpectrumMarker, Math.Clamp(x - (SpectrumMarker.Width / 2d), 0d, SpectrumSurface.ActualWidth - SpectrumMarker.Width));
        Canvas.SetTop(SpectrumMarker, Math.Clamp(y - (SpectrumMarker.Height / 2d), 0d, SpectrumSurface.ActualHeight - SpectrumMarker.Height));
        SpectrumMarker.Visibility = Visibility.Visible;
    }

    private void UpdateSelection(string color, bool updateHexInput)
    {
        SelectedColor = color;
        SelectedColorPreview.Background = (Brush)new BrushConverter().ConvertFromString(color)!;
        ValidationMessage.Text = "Ready to apply.";
        ValidationMessage.Foreground = (Brush)FindResource("SuccessBrush");

        if (!updateHexInput)
        {
            return;
        }

        _isUpdatingHex = true;
        HexInput.Text = color;
        HexInput.CaretIndex = HexInput.Text.Length;
        _isUpdatingHex = false;
    }

    private void OnChooseColor(object sender, RoutedEventArgs eventArgs)
    {
        if (!ColorValueConverter.TryNormalizeHex(HexInput.Text, out string normalized))
        {
            ValidationMessage.Text = "Enter a valid six-digit hex color before continuing.";
            ValidationMessage.Foreground = (Brush)FindResource("DangerBrush");
            HexInput.Focus();
            return;
        }

        SelectedColor = normalized;
        DialogResult = true;
    }
}
