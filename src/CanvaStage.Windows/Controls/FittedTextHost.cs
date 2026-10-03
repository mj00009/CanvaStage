using System.Windows;
using System.Windows.Controls;
using Size = System.Windows.Size;

namespace CanvaStage.Windows.Controls;

/// <summary>Fits during layout, before the new text can be painted at an oversized font.</summary>
public sealed class FittedTextHost : Decorator
{
    public static readonly DependencyProperty RequestedFontSizeProperty = DependencyProperty.Register(
        nameof(RequestedFontSize), typeof(double), typeof(FittedTextHost),
        new FrameworkPropertyMetadata(72d, FrameworkPropertyMetadataOptions.AffectsMeasure), IsValidFontSize);
    public static readonly DependencyProperty MinimumFontSizeProperty = DependencyProperty.Register(
        nameof(MinimumFontSize), typeof(double), typeof(FittedTextHost),
        new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsMeasure), IsValidFontSize);

    public double RequestedFontSize
    {
        get => (double)GetValue(RequestedFontSizeProperty);
        set => SetValue(RequestedFontSizeProperty, value);
    }
    public double MinimumFontSize
    {
        get => (double)GetValue(MinimumFontSizeProperty);
        set => SetValue(MinimumFontSizeProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is null || availableSize.Width <= 0 || availableSize.Height <= 0)
            return base.MeasureOverride(availableSize);
        if (Child is not TextBlock text)
            throw new InvalidOperationException("FittedTextHost requires a TextBlock child.");

        double requested = RequestedFontSize;
        double minimum = Math.Min(MinimumFontSize, requested);
        Size naturalSize = MeasureAt(text, requested, availableSize.Width);
        if (!Fits(naturalSize, availableSize))
        {
            double low = minimum;
            double high = requested;
            double best = minimum;
            for (int iteration = 0; iteration < 12; iteration++)
            {
                double candidate = (low + high) / 2;
                if (Fits(MeasureAt(text, candidate, availableSize.Width), availableSize))
                {
                    best = candidate;
                    low = candidate;
                }
                else high = candidate;
            }
            naturalSize = MeasureAt(text, best, availableSize.Width);
        }
        NaturalTextSize = naturalSize;

        // Measure the final size with the same TextBlock renderer, typeface and DPI
        // used to draw it. No dispatcher callback or temporary scaling is needed.
        return base.MeasureOverride(availableSize);
    }

    internal Size NaturalTextSize { get; private set; }
    internal bool TextFits => Fits(NaturalTextSize, new Size(ActualWidth, ActualHeight));

    private static Size MeasureAt(TextBlock text, double fontSize, double width)
    {
        text.SetCurrentValue(TextBlock.FontSizeProperty, fontSize);
        // Unbounded height reveals the full wrapped text instead of a clipped height.
        text.Measure(new Size(width, double.PositiveInfinity));
        return text.DesiredSize;
    }

    private static bool Fits(Size desired, Size available)
        => desired.Width <= available.Width && desired.Height <= available.Height;

    private static bool IsValidFontSize(object value)
        => TextBlock.FontSizeProperty.IsValidValue(value);
}
