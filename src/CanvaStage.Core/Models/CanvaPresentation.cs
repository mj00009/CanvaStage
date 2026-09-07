namespace CanvaStage.Core.Models;

public sealed record CanvaPresentation(
    string Id,
    string Title,
    int PageCount,
    string? ThumbnailUrl = null,
    DateTimeOffset? ModifiedAt = null);
