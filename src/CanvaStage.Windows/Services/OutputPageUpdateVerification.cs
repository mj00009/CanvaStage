using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TextBlock = System.Windows.Controls.TextBlock;

namespace CanvaStage.Windows.Services;

/// <summary>Detects stale output drawing even when bindings and Presenter status are current.</summary>
internal static class OutputPageUpdateVerification
{
    public static async Task RunAsync(MainWindow mainWindow, MainViewModel mainViewModel,
        OutputWindow output, IOutputCoordinator coordinator, string directory,
        Action<FrameworkElement, string, double> capture)
    {
        PresentationDocument originalDocument = coordinator.Snapshot.Document;
        int originalPage = coordinator.Snapshot.SelectedPageNumber;
        MonitorAppearance originalAppearance = coordinator.Snapshot.Appearance;
        bool originalSyncEnabled = mainViewModel.IsPresenterSyncEnabled;
        PresentationPage[] pages = Enumerable.Range(1, 87).Select(page => new PresentationPage(page, $"Marker {page}",
            [$"Primary marker {page:00}: this slide must change on the output display.",
             "A second line tests wrapping without relying on changing panel dimensions."], "#080B11", "#8454F4")).ToArray();
        TextBlock text = (TextBlock)output.FindName("PrimaryText");
        FrameworkElement host = (FrameworkElement)output.FindName("PrimaryTextHost");
        string? previousPixels = null;
        void Feed(int page) => mainViewModel.ReceivePresenterUpdateForVerification(
            new(CanvaPresenterTrackingState.PageDetected, page, 87, "Synthetic Presenter", DateTimeOffset.UtcNow));

        async Task ObservePageAsync(int expectedPage, Action change, bool pixelsShouldChange = true)
        {
            TaskCompletionSource frame = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnFrame(object? sender, EventArgs args) => frame.TrySetResult();
            CompositionTarget.Rendering += OnFrame;
            try
            {
                change();
                await frame.Task.WaitAsync(TimeSpan.FromSeconds(5));
                // Let ordinary binding, fit and layout work settle. Do not call UpdateLayout,
                // InvalidateVisual or the fitter to repair the very path under test.
                await output.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                if (coordinator.Snapshot.SelectedPageNumber != expectedPage || mainViewModel.CurrentPageNumber != expectedPage
                    || ((OutputWindowViewModel)output.DataContext).Snapshot.SelectedPageNumber != expectedPage
                    || text.Text != string.Join(Environment.NewLine, pages[expectedPage - 1].TextBlocks))
                    throw new InvalidOperationException($"Output binding did not follow expected page {expectedPage}.");
                string pixels = CaptureDrawingFingerprint(host);
                if (previousPixels is not null && ((pixels == previousPixels) == pixelsShouldChange))
                    throw new InvalidOperationException($"Output drawing {(pixelsShouldChange ? "stayed stale" : "changed while sync was paused")} at page {expectedPage}.");
                previousPixels = pixels;
                output.VerifyTextFit();
            }
            finally { CompositionTarget.Rendering -= OnFrame; }
        }

        try
        {
            coordinator.SetDocument(new("output-update-verification", "Output drawing verification", pages, DateTimeOffset.UtcNow));
            coordinator.SetAppearance(MonitorAppearance.Default with { FontSize = 160, ShowClock = false });
            mainViewModel.IsPresenterSyncEnabled = true;
            for (int page = 1; page <= 40; page++) await ObservePageAsync(page, () => Feed(page));
            await ObservePageAsync(40, () => { mainViewModel.TogglePresenterSyncCommand.Execute(null); Feed(41); }, pixelsShouldChange: false);
            await ObservePageAsync(41, () => mainViewModel.NextPageCommand.Execute(null));
            await ObservePageAsync(41, () => Feed(42), pixelsShouldChange: false);
            await ObservePageAsync(42, () => mainViewModel.TogglePresenterSyncCommand.Execute(null));
            await ObservePageAsync(14, () => Feed(14));
            await ObservePageAsync(21, () => Feed(21));
            capture(output, Path.Combine(directory, "output-following-page-21.png"), 1);
            capture(mainWindow, Path.Combine(directory, "operator-following-page-21.png"), 1);
        }
        finally
        {
            mainViewModel.IsPresenterSyncEnabled = false;
            coordinator.SetDocument(originalDocument, originalPage);
            coordinator.SetAppearance(originalAppearance);
            mainViewModel.IsPresenterSyncEnabled = originalSyncEnabled;
            if (originalSyncEnabled)
                mainViewModel.ReceivePresenterUpdateForVerification(new(CanvaPresenterTrackingState.PageDetected,
                    originalPage, originalDocument.Pages.Count, "Synthetic Presenter", DateTimeOffset.UtcNow));
        }
    }

    private static string CaptureDrawingFingerprint(FrameworkElement element)
    {
        int width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
        int height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        byte[] pixels = new byte[checked(width * height * 4)];
        bitmap.CopyPixels(pixels, checked(width * 4), 0);
        return Convert.ToHexString(SHA256.HashData(pixels));
    }
}
