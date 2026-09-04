namespace CanvaStage.Core.Models;

public sealed record PresentationPage(
    int Number,
    string Title,
    IReadOnlyList<string> TextBlocks,
    string MirrorBackground,
    string MirrorAccent,
    bool HasFormattedText = true,
    string? FallbackImagePath = null,
    string? ExternalPageId = null)
{
    public string PrimaryText => TextBlocks.FirstOrDefault(static text => !string.IsNullOrWhiteSpace(text)) ?? string.Empty;
}
