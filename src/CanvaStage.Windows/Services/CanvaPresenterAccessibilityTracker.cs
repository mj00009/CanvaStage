using CanvaStage.Core.Utilities;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace CanvaStage.Windows.Services;

public sealed class CanvaPresenterAccessibilityTracker : IDisposable
{
    private const int MaximumElementsPerWindow = 3000;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(750);
    private static readonly HashSet<string> BrowserProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "brave",
        "chrome",
        "firefox",
        "msedge",
        "opera"
    };

    private readonly object _syncRoot = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _trackingTask;
    private string? _lastUpdateSignature;

    public event EventHandler<CanvaPresenterTrackingUpdate>? TrackingUpdated;

    public bool IsTracking
    {
        get
        {
            lock (_syncRoot)
            {
                return _cancellationTokenSource is not null;
            }
        }
    }

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_cancellationTokenSource is not null)
            {
                return;
            }

            _lastUpdateSignature = null;
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = _cancellationTokenSource.Token;
            _trackingTask = Task.Run(() => TrackAsync(cancellationToken), CancellationToken.None);
        }

        Publish(new CanvaPresenterTrackingUpdate(
            CanvaPresenterTrackingState.Scanning,
            null,
            null,
            "Watching Windows accessibility data for a Canva presenter page counter…",
            DateTimeOffset.UtcNow));
    }

    public void Stop()
    {
        CancellationTokenSource? cancellationTokenSource;
        lock (_syncRoot)
        {
            cancellationTokenSource = _cancellationTokenSource;
            _cancellationTokenSource = null;
            _trackingTask = null;
            _lastUpdateSignature = null;
        }

        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
    }

    public void Dispose() => Stop();

    private async Task TrackAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                CanvaPresenterTrackingUpdate update = ScanAccessibilityTree();
                PublishIfChanged(update);
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PrivacySafeLog.WriteException("Canva presenter accessibility tracker failed", exception);
            MarkStoppedAfterFailure(cancellationToken);
            Publish(new CanvaPresenterTrackingUpdate(
                CanvaPresenterTrackingState.Error,
                null,
                null,
                "Windows accessibility scanning stopped unexpectedly. Review the CanvaStage log, then try again.",
                DateTimeOffset.UtcNow));
        }
    }

    private void MarkStoppedAfterFailure(CancellationToken cancellationToken)
    {
        CancellationTokenSource? cancellationTokenSource = null;
        lock (_syncRoot)
        {
            if (_cancellationTokenSource is not null && _cancellationTokenSource.Token == cancellationToken)
            {
                cancellationTokenSource = _cancellationTokenSource;
                _cancellationTokenSource = null;
                _trackingTask = null;
            }
        }

        cancellationTokenSource?.Dispose();
    }

    private static CanvaPresenterTrackingUpdate ScanAccessibilityTree()
    {
        try
        {
            AutomationElementCollection windows = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
            int candidateCount = 0;
            for (int index = 0; index < windows.Count; index++)
            {
                AutomationElement window = windows[index];
                if (!TryGetCandidateApplication(window, out string applicationName))
                {
                    continue;
                }

                candidateCount++;
                IReadOnlyList<string> accessibleText = ReadAccessibleText(window);
                if (PresenterPageCounterParser.TryParse(accessibleText, out int currentPage, out int totalPages))
                {
                    return new CanvaPresenterTrackingUpdate(
                        CanvaPresenterTrackingState.PageDetected,
                        currentPage,
                        totalPages,
                        $"Detected presenter page {currentPage} of {totalPages} from {applicationName}.",
                        DateTimeOffset.UtcNow);
                }
            }

            return candidateCount == 0
                ? new CanvaPresenterTrackingUpdate(
                    CanvaPresenterTrackingState.NoPresenterWindow,
                    null,
                    null,
                    "No Canva presenter window was found. Start Presenter View in Canva, then leave this scan running.",
                    DateTimeOffset.UtcNow)
                : new CanvaPresenterTrackingUpdate(
                    CanvaPresenterTrackingState.CounterUnavailable,
                    null,
                    null,
                    "A possible Canva presenter window was found, but Windows did not expose a page counter. This result means the image-matching fallback may be needed.",
                    DateTimeOffset.UtcNow);
        }
        catch (UnauthorizedAccessException)
        {
            return new CanvaPresenterTrackingUpdate(
                CanvaPresenterTrackingState.AccessDenied,
                null,
                null,
                "Windows blocked accessibility access. Run CanvaStage and Canva at the same permission level, then try again.",
                DateTimeOffset.UtcNow);
        }
        catch (ElementNotAvailableException)
        {
            return new CanvaPresenterTrackingUpdate(
                CanvaPresenterTrackingState.Scanning,
                null,
                null,
                "The Canva window changed while it was being inspected. Continuing to watch…",
                DateTimeOffset.UtcNow);
        }
        catch (COMException)
        {
            return new CanvaPresenterTrackingUpdate(
                CanvaPresenterTrackingState.Scanning,
                null,
                null,
                "The Canva window changed while it was being inspected. Continuing to watch…",
                DateTimeOffset.UtcNow);
        }
    }

    private static bool TryGetCandidateApplication(AutomationElement window, out string applicationName)
    {
        applicationName = "Canva";
        try
        {
            int processId = window.Current.ProcessId;
            if (processId == Environment.ProcessId)
            {
                return false;
            }

            using Process process = Process.GetProcessById(processId);
            string processName = process.ProcessName;
            string windowName = window.Current.Name ?? string.Empty;
            bool isCanvaProcess = processName.Contains("canva", StringComparison.OrdinalIgnoreCase);
            bool isBrowser = BrowserProcessNames.Contains(processName);
            bool titleLooksRelevant = windowName.Contains("canva", StringComparison.OrdinalIgnoreCase)
                || windowName.Contains("present", StringComparison.OrdinalIgnoreCase);

            if (!isCanvaProcess && !isBrowser)
            {
                return false;
            }

            if (!isCanvaProcess && !titleLooksRelevant && !string.IsNullOrWhiteSpace(windowName))
            {
                return false;
            }

            applicationName = isCanvaProcess ? "the Canva desktop app" : GetBrowserDisplayName(processName);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> ReadAccessibleText(AutomationElement window)
    {
        var values = new List<string>();
        var pending = new Queue<AutomationElement>();
        TreeWalker walker = TreeWalker.ControlViewWalker;
        AutomationElement? firstChild = TryGetFirstChild(walker, window);
        if (firstChild is not null)
        {
            pending.Enqueue(firstChild);
        }

        int visited = 0;
        while (pending.Count > 0 && visited < MaximumElementsPerWindow)
        {
            AutomationElement element = pending.Dequeue();
            visited++;

            try
            {
                string name = element.Current.Name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    values.Add(name);
                }
            }
            catch (ElementNotAvailableException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (COMException)
            {
            }

            AutomationElement? child = TryGetFirstChild(walker, element);
            if (child is not null)
            {
                pending.Enqueue(child);
            }

            AutomationElement? sibling = TryGetNextSibling(walker, element);
            if (sibling is not null)
            {
                pending.Enqueue(sibling);
            }
        }

        return values;
    }

    private static AutomationElement? TryGetFirstChild(TreeWalker walker, AutomationElement element)
    {
        try
        {
            return walker.GetFirstChild(element);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static AutomationElement? TryGetNextSibling(TreeWalker walker, AutomationElement element)
    {
        try
        {
            return walker.GetNextSibling(element);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private void PublishIfChanged(CanvaPresenterTrackingUpdate update)
    {
        string signature = $"{update.State}|{update.CurrentPage}|{update.TotalPages}|{update.Message}";
        lock (_syncRoot)
        {
            if (signature == _lastUpdateSignature)
            {
                return;
            }

            _lastUpdateSignature = signature;
        }

        Publish(update);
    }

    private void Publish(CanvaPresenterTrackingUpdate update)
    {
        try
        {
            TrackingUpdated?.Invoke(this, update);
        }
        catch (Exception exception)
        {
            PrivacySafeLog.WriteException("Canva presenter accessibility update handler failed", exception);
        }
    }

    private static string GetBrowserDisplayName(string processName) => processName.ToLowerInvariant() switch
    {
        "brave" => "Brave",
        "chrome" => "Chrome",
        "firefox" => "Firefox",
        "msedge" => "Microsoft Edge",
        "opera" => "Opera",
        _ => "the browser"
    };
}

public enum CanvaPresenterTrackingState
{
    Scanning,
    PageDetected,
    NoPresenterWindow,
    CounterUnavailable,
    AccessDenied,
    Error
}

public sealed record CanvaPresenterTrackingUpdate(
    CanvaPresenterTrackingState State,
    int? CurrentPage,
    int? TotalPages,
    string Message,
    DateTimeOffset ObservedAt);
