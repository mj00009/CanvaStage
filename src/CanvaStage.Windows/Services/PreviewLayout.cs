namespace CanvaStage.Windows.Services;

/// <summary>Fits a whole output frame without changing its aspect ratio.</summary>
internal static class PreviewLayout
{
    public static (double Width, double Height) Fit(double availableWidth, double availableHeight,
        double sourceWidth, double sourceHeight)
    {
        double ratio = double.IsFinite(sourceWidth) && double.IsFinite(sourceHeight)
            && sourceWidth > 0 && sourceHeight > 0 ? sourceWidth / sourceHeight : 16d / 9d;
        double width = double.IsPositiveInfinity(availableWidth) ? 640 : Math.Max(0, availableWidth);
        if (double.IsFinite(availableHeight)) width = Math.Min(width, Math.Max(0, availableHeight) * ratio);
        return (width, width / ratio);
    }
}
