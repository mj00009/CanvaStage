namespace CanvaStage.Windows.Services;

internal static class WheelScrollPolicy
{
    public static double Target(double current, double delta, double maximum, int lines, double viewport)
    {
        if (lines == 0) return Math.Clamp(current, 0, maximum);
        double distance = lines < 0 ? viewport : lines * 16d;
        return Math.Clamp(current - delta / 120d * distance, 0, maximum);
    }
}
