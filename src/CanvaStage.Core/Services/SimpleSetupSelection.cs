using CanvaStage.Core.Models;

namespace CanvaStage.Core.Services;

/// <summary>Never choose the first result when an operator's default is missing or ambiguous.</summary>
public static class SimpleSetupSelection
{
    public static CanvaPresentation? Resolve(IReadOnlyList<CanvaPresentation> presentations, string? defaultId)
    {
        if (string.IsNullOrWhiteSpace(defaultId)) return null;
        IEnumerable<CanvaPresentation> matches = presentations.Where(item => item.Id == defaultId);
        CanvaPresentation[] candidates = matches.Take(2).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    public static bool CanReuseCache(PresentationDocument document, CanvaPresentation metadata, bool hasAllImages, bool matchSlideCapitalization = false)
        => document.Id == metadata.Id && document.UsesRenderedPageImages && hasAllImages
            && (!matchSlideCapitalization || document.WasSlideCapitalizationChecked)
            && document.SourceModifiedAt is not null && metadata.ModifiedAt is not null
            && document.SourceModifiedAt == metadata.ModifiedAt;
}
