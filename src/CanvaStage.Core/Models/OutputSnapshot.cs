namespace CanvaStage.Core.Models;

public sealed record OutputSnapshot(
    bool IsLive,
    PresentationDocument Document,
    int SelectedPageNumber,
    PresentationPage? CurrentPage,
    string? NextLine,
    OutputMode Mode,
    MonitorLayout Layout,
    NextLineOption NextLineOption,
    MonitorAppearance Appearance,
    bool CanGoPrevious,
    bool CanGoNext);
