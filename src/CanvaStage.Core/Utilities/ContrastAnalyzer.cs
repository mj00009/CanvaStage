namespace CanvaStage.Core.Utilities;

public static class ContrastAnalyzer
{
    public static double GetContrastRatio(string foreground, string background)
    {
        if (!TryParseHex(foreground, out (byte Red, byte Green, byte Blue) foregroundColor) ||
            !TryParseHex(background, out (byte Red, byte Green, byte Blue) backgroundColor))
        {
            return 0;
        }

        double light = RelativeLuminance(foregroundColor);
        double dark = RelativeLuminance(backgroundColor);
        return (Math.Max(light, dark) + 0.05) / (Math.Min(light, dark) + 0.05);
    }

    public static bool HasRecommendedContrast(string foreground, string background) => GetContrastRatio(foreground, background) >= 4.5;

    private static bool TryParseHex(string value, out (byte Red, byte Green, byte Blue) color)
    {
        color = default;
        string normalized = value.Trim().TrimStart('#');
        if (normalized.Length != 6 || !byte.TryParse(normalized[..2], System.Globalization.NumberStyles.HexNumber, null, out byte red) ||
            !byte.TryParse(normalized[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte green) ||
            !byte.TryParse(normalized[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte blue))
        {
            return false;
        }

        color = (red, green, blue);
        return true;
    }

    private static double RelativeLuminance((byte Red, byte Green, byte Blue) color)
    {
        return 0.2126 * Normalize(color.Red) + 0.7152 * Normalize(color.Green) + 0.0722 * Normalize(color.Blue);
    }

    private static double Normalize(byte component)
    {
        double value = component / 255d;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
