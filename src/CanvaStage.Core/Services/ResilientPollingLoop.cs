namespace CanvaStage.Core.Services;

/// <summary>A single cancellable worker retries failed scans without stopping the application.</summary>
public static class ResilientPollingLoop
{
    public static TimeSpan ScanInterval { get; } = TimeSpan.FromMilliseconds(200);
    public static TimeSpan RetryDelay(int failures) => TimeSpan.FromSeconds(Math.Min(15, Math.Pow(2, Math.Clamp(failures - 1, 0, 4))));

    public static async Task RunAsync<T>(Func<CancellationToken, Task<T>> scan, Action<T> publish,
        Action<Exception, TimeSpan> recover, CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= Task.Delay;
        int failures = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan interval = ScanInterval;
                try
                {
                    T result = await scan(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    publish(result);
                    failures = 0;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    interval = RetryDelay(++failures);
                    recover(exception, interval);
                }
                await delay(interval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { /* Normal shutdown. */ }
    }
}
