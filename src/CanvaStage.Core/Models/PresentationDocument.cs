namespace CanvaStage.Core.Models;

public sealed record PresentationDocument(
    string Id,
    string Title,
    IReadOnlyList<PresentationPage> Pages,
    DateTimeOffset RefreshedAt,
    DateTimeOffset? SourceModifiedAt = null,
    bool UsesRenderedPageImages = false,
    bool WasSlideCapitalizationChecked = false,
    int CapitalizationAdjustedPages = 0,
    int CapitalizationUnchangedPages = 0,
    int CapitalizationUnavailablePages = 0)
{
    public PresentationDocument WithSlideCapitalization(bool enabled) => Pages.All(page => page.UseSlideCapitalization == enabled) ? this : this with
    {
        Pages = Pages.Select(page => page.UseSlideCapitalization == enabled ? page : page with { UseSlideCapitalization = enabled }).ToArray()
    };

    public PresentationPage? FindPage(int pageNumber) => Pages.FirstOrDefault(page => page.Number == pageNumber);

    public PresentationPage? FindPageByExternalId(string pageId) =>
        Pages.FirstOrDefault(page => string.Equals(page.ExternalPageId, pageId, StringComparison.Ordinal));
}
