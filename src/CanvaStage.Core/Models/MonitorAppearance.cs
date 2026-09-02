namespace CanvaStage.Core.Models;

public sealed record MonitorAppearance(
    string BackgroundColor,
    string TextColor,
    string AccentColor,
    string FontFamily,
    double FontSize,
    bool ShowClock)
{
    public static MonitorAppearance Default { get; } = new("#080B11", "#F8F8FC", "#8454F4", "Aptos", 72, true);
}
