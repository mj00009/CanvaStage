namespace CanvaStage.Core.Models;

public sealed record MonitorAppearance(
    string BackgroundColor,
    string TextColor,
    string AccentColor,
    string FontFamily,
    double FontSize,
    bool ShowClock,
    double NextLineFontSize = 30,
    bool IsNextLineBold = false,
    string NextLineColor = "#F8F8FC",
    bool ShowMirrorClock = false,
    double ClockFontSize = 28,
    double MirrorClockFontSize = 28,
    bool IsClockBold = true,
    bool IsMirrorClockOnLeft = false,
    bool IsSlideFadeEnabled = false,
    double SlideFadeMilliseconds = 250)
{
    public static MonitorAppearance Default { get; } = new("#080B11", "#F8F8FC", "#8454F4", "Aptos", 72, true, 30, false, "#F8F8FC");

    public MonitorAppearance Normalize() => this with
    {
        ClockFontSize = double.IsFinite(ClockFontSize) ? Math.Clamp(ClockFontSize, 16, 96) : 28,
        MirrorClockFontSize = double.IsFinite(MirrorClockFontSize) ? Math.Clamp(MirrorClockFontSize, 16, 96) : 28,
        SlideFadeMilliseconds = double.IsFinite(SlideFadeMilliseconds) ? Math.Clamp(SlideFadeMilliseconds, 100, 1000) : 250
    };
}
