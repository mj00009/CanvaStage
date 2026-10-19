using System.Text.Json.Serialization;

namespace CanvaStage.Core.Models;

public sealed record PresentationPage(
    int Number,
    string Title,
    IReadOnlyList<string> TextBlocks,
    string MirrorBackground,
    string MirrorAccent,
    bool HasFormattedText = true,
    string? FallbackImagePath = null,
    string? ExternalPageId = null,
    IReadOnlyList<string>? SlideCapitalizationTextBlocks = null,
    bool UseSlideCapitalization = false)
{
    [JsonIgnore]
    public IReadOnlyList<string> DisplayTextBlocks => UseSlideCapitalization && SlideCapitalizationTextBlocks is { } matched && matched.Count == TextBlocks.Count ? matched : TextBlocks;
    public string PrimaryText => DisplayTextBlocks.FirstOrDefault(static text => !string.IsNullOrWhiteSpace(text)) ?? string.Empty;
    [JsonIgnore]
    public string DisplayTitle => UseSlideCapitalization && Title == TextBlocks.FirstOrDefault() ? DisplayTextBlocks.FirstOrDefault() ?? Title : Title;
}
