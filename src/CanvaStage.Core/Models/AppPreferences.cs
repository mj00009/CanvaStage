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
    string? SelectedPresentationTitle = null,
    DateTimeOffset? SelectedPresentationRefreshedAt = null,
    bool IsPresenterSyncEnabled = true,
    bool IsAutomaticContentRefreshEnabled = true,
    string? LastAppliedPresentationId = null,
    string? LastAppliedPresentationTitle = null,
    bool MatchSlideCapitalization = false)
{
    public static AppPreferences Default { get; } = new(null, 1, OutputMode.Monitor, MonitorLayout.CurrentAndNext, NextLineOption.FirstNonEmptyLine, MonitorAppearance.Default, false, IsPresenterSyncEnabled: true);
}
