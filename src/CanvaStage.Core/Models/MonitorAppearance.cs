namespace CanvaStage.Core.Models;

public sealed record MonitorAppearance(
    string BackgroundColor,
    string TextColor,
    string AccentColor,
    string FontFamily,
    double FontSize,
    bool ShowClock)
{
    public static MonitorAppearance Default { get; } = new("#101113", "#F7F8FA", "#75E6C8", "Aptos", 72, true);
}
