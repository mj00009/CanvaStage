using CanvaStage.Core.Models;

namespace CanvaStage.Core.Services;

public interface ICanvaPresentationProvider : IPresentationProvider
{
    Task<IReadOnlyList<CanvaPresentation>> ListPresentationsAsync(CancellationToken cancellationToken = default);
    Task<PresentationDocument> LoadAsync(string presentationId, CancellationToken cancellationToken = default);
}
