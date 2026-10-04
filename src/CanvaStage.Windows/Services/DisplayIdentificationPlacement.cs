using System.Drawing;

namespace CanvaStage.Windows.Services;

internal static class DisplayIdentificationPlacement
{
    /// <summary>Centers a 360 × 280 DIP label within physical monitor bounds.</summary>
    public static Rectangle GetBounds(Rectangle monitor, double scale)
    {
        if (monitor.Width <= 0 || monitor.Height <= 0 || !double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(monitor), "Monitor bounds and DPI scale must be positive.");
        int marginX = (int)Math.Min(20 * scale, monitor.Width / 4d);
        int marginY = (int)Math.Min(20 * scale, monitor.Height / 4d);
        int width = (int)Math.Min(Math.Round(360 * scale), monitor.Width - 2 * marginX);
        int height = (int)Math.Min(Math.Round(280 * scale), monitor.Height - 2 * marginY);
        return new Rectangle(monitor.Left + (monitor.Width - width) / 2, monitor.Top + (monitor.Height - height) / 2,
            Math.Max(1, width), Math.Max(1, height));
    }
}
