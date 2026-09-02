using CanvaStage.Core.Models;

namespace CanvaStage.Core.Services;

public interface IPresentationProvider
{
    Task<PresentationDocument> LoadAsync(CancellationToken cancellationToken = default);
}
