using CanvaStage.Windows.Services;
using System.Windows;
using Decorator = System.Windows.Controls.Decorator;
using Size = System.Windows.Size;

namespace CanvaStage.Windows.Controls;

/// <summary>Sizes the operator container to the shared output, never reflowing that output.</summary>
public sealed class AspectRatioPreviewHost : Decorator
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(FrameworkElement), typeof(AspectRatioPreviewHost),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnSourceChanged));

    public AspectRatioPreviewHost()
    {
        Loaded += (_, _) => Subscribe();
        Unloaded += (_, _) => Unsubscribe();
    }

    private FrameworkElement? _observedSource;

    public FrameworkElement? Source
    {
        get => (FrameworkElement?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        (double width, double height) = PreviewLayout.Fit(constraint.Width, constraint.Height,
            Source?.ActualWidth ?? 0, Source?.ActualHeight ?? 0);
        Size size = new(width, height);
        Child?.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        (double width, double height) = PreviewLayout.Fit(arrangeSize.Width, arrangeSize.Height,
            Source?.ActualWidth ?? 0, Source?.ActualHeight ?? 0);
        Child?.Arrange(new Rect((arrangeSize.Width - width) / 2, (arrangeSize.Height - height) / 2, width, height));
        return arrangeSize;
    }

    private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        AspectRatioPreviewHost host = (AspectRatioPreviewHost)sender;
        host.Unsubscribe();
        if (host.IsLoaded) host.Subscribe();
    }

    private void Subscribe()
    {
        if (ReferenceEquals(_observedSource, Source)) return;
        Unsubscribe();
        _observedSource = Source;
        if (_observedSource is not null) _observedSource.SizeChanged += OnSourceSizeChanged;
        InvalidateMeasure();
    }

    private void Unsubscribe()
    {
        if (_observedSource is not null) _observedSource.SizeChanged -= OnSourceSizeChanged;
        _observedSource = null;
    }

    private void OnSourceSizeChanged(object sender, SizeChangedEventArgs args) => InvalidateMeasure();
}
