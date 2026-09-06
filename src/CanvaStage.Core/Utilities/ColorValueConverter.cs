using System.Globalization;
using System.Text.RegularExpressions;

namespace CanvaStage.Core.Utilities;

public static partial class ColorValueConverter
{
    public static bool TryNormalizeHex(string? value, out string normalized)
    {
        string candidate = value?.Trim() ?? string.Empty;
        if (!candidate.StartsWith('#'))
        {
            candidate = $"#{candidate}";
        }

        if (!HexColorRegex().IsMatch(candidate))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = candidate.ToUpperInvariant();
        return true;
    }

    public static string HsvToHex(double hue, double saturation, double value)
    {
        double normalizedHue = ((hue % 360d) + 360d) % 360d;
        double normalizedSaturation = Math.Clamp(saturation, 0d, 1d);
        double normalizedValue = Math.Clamp(value, 0d, 1d);
        double chroma = normalizedValue * normalizedSaturation;
        double hueSection = normalizedHue / 60d;
        double secondary = chroma * (1d - Math.Abs((hueSection % 2d) - 1d));

        (double red, double green, double blue) = hueSection switch
        {
            < 1d => (chroma, secondary, 0d),
            < 2d => (secondary, chroma, 0d),
            < 3d => (0d, chroma, secondary),
            < 4d => (0d, secondary, chroma),
            < 5d => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary)
        };

        double match = normalizedValue - chroma;
        int redByte = (int)Math.Round((red + match) * 255d, MidpointRounding.AwayFromZero);
        int greenByte = (int)Math.Round((green + match) * 255d, MidpointRounding.AwayFromZero);
        int blueByte = (int)Math.Round((blue + match) * 255d, MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture, $"#{redByte:X2}{greenByte:X2}{blueByte:X2}");
    }

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();
}
