namespace CanvaStage.Core.Models;

public sealed record PresentationDocument(
    string Id,
    string Title,
    IReadOnlyList<PresentationPage> Pages,
    DateTimeOffset RefreshedAt)
{
    public PresentationPage? FindPage(int pageNumber) => Pages.FirstOrDefault(page => page.Number == pageNumber);
}
