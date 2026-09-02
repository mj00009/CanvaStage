namespace CanvaStage.Core.Models;

public sealed record AppPreferences(
    string? SelectedDisplayId,
    int SelectedPage,
    OutputMode OutputMode,
    MonitorLayout MonitorLayout,
    NextLineOption NextLineOption,
    MonitorAppearance Appearance,
    bool IsOperatorTopmost,
    string? SelectedPresentationId = null,
    string? SelectedPresentationTitle = null)
{
    public static AppPreferences Default { get; } = new(null, 1, OutputMode.Monitor, MonitorLayout.CurrentAndNext, NextLineOption.FirstNonEmptyLine, MonitorAppearance.Default, false);
}
