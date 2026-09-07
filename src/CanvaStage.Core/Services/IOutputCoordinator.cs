using CanvaStage.Core.Models;

namespace CanvaStage.Core.Services;

public interface IOutputCoordinator
{
    event EventHandler<OutputSnapshot>? SnapshotChanged;

    OutputSnapshot Snapshot { get; }
    void StartOutput();
    void StopOutput();
    void SetBlanked(bool isBlanked);
    void SelectPage(int pageNumber);
    void GoPrevious();
    void GoNext();
    void SetOutputMode(OutputMode mode);
    void SetLayout(MonitorLayout layout);
    void SetNextLineOption(NextLineOption option);
    void SetAppearance(MonitorAppearance appearance);
    void SetDocument(PresentationDocument document, int? preferredPageNumber = null);
}
