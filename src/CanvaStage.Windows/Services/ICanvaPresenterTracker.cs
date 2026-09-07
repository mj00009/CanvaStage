namespace CanvaStage.Windows.Services;

public interface ICanvaPresenterTracker : IDisposable
{
    event EventHandler<CanvaPresenterTrackingUpdate>? TrackingUpdated;
    bool IsTracking { get; }
    long Generation { get; }
    void Start();
    void Stop();
}
