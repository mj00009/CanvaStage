using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Core.Utilities;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace CanvaStage.Windows.Services;

/// <summary>Deterministic Windows-only acceptance checks, using synthetic content and isolated storage.</summary>
internal static class WindowsReleaseVerification
{
    internal sealed class DemoTracker : ICanvaPresenterTracker
    {
        public event EventHandler<CanvaPresenterTrackingUpdate>? TrackingUpdated { add { } remove { } }
        public bool IsTracking { get; private set; }
        public long Generation { get; private set; }
        public void Start() { IsTracking = true; Generation++; }
        public void Stop() { IsTracking = false; Generation++; }
        public void Dispose() => Stop();
    }

    public static async Task RunAsync(MainWindow mainWindow, MainViewModel viewModel, IOutputCoordinator coordinator,
        string directory, Action<FrameworkElement, string, double> capture)
    {
        Directory.CreateDirectory(directory);
        List<string> checks = [];
        using BindingErrors errors = new();
        PresentationDocument document = CreateDocument();
        coordinator.SetDocument(document);
        void Feed(int page, int total = 3) => viewModel.ReceivePresenterUpdateForVerification(
            new(CanvaPresenterTrackingState.PageDetected, page, total, "Synthetic Presenter", DateTimeOffset.UtcNow));

        viewModel.IsPresenterSyncEnabled = true;
        Feed(2);
        Require(viewModel.CurrentPageNumber == 2, "Presenter counter selects its page directly");
        viewModel.PreviousPageCommand.Execute(null);
        Feed(3);
        Require(viewModel.CurrentPageNumber == 1 && viewModel.PresenterSyncSummary.StartsWith("Paused"), "Manual navigation pauses following");
        viewModel.TogglePresenterSyncCommand.Execute(null);
        Require(viewModel.CurrentPageNumber == 3, "Resume uses current fresh Presenter page");
        Feed(1, 4);
        Require(viewModel.CurrentPageNumber == 3 && viewModel.PresenterSyncSummary == "Presentation mismatch", "Mismatched page count holds the last page");
        Feed(2);
        Require(viewModel.CurrentPageNumber == 2 && viewModel.PresenterSyncSummary == "Following Canva", "Matching counter resumes direct following");
        viewModel.IsPresenterSyncEnabled = false;
        Feed(3);
        Require(viewModel.CurrentPageNumber == 2, "Disabled sync ignores queued updates");
        viewModel.IsPresenterSyncEnabled = true;
        viewModel.ReceivePresenterUpdateForVerification(new(CanvaPresenterTrackingState.Error, null, null, "Retrying automatically…", DateTimeOffset.UtcNow));
        Require(viewModel.PresenterSyncSummary == "Reconnecting to Presenter", "Scan failure has actionable recovery status");
        Feed(2);
        Require(viewModel.PresenterSyncSummary == "Following Canva", "Recovered scan resumes following");
        checks.Add("Direct Presenter counter following, page-count guard, manual pause, resume, disabled updates and recovery");

        coordinator.StartOutput();
        viewModel.BlankOutputCommand.Execute(null);
        Require(coordinator.Snapshot is { IsLive: true, IsBlanked: true }, "Blank action keeps the output session live while hiding content");
        viewModel.BlankOutputCommand.Execute(null);
        Require(!coordinator.Snapshot.IsBlanked, "Restore action returns live content");
        checks.Add("Prominent blank and restore output action");

        viewModel.NextLineFontSize = 48;
        viewModel.IsNextLineBold = true;
        viewModel.NextLineColor = "#75E6C8";
        Require(coordinator.Snapshot.Appearance is { NextLineFontSize: 48, IsNextLineBold: true, NextLineColor: "#75E6C8" },
            "Next-line size, weight and color apply immediately");
        checks.Add("Custom next-line size, weight and color apply to Preview and output");
        Require(mainWindow.Icon is not null, "Operator window loads the CanvaStage icon");
        Require(System.Windows.Application.Current.ShutdownMode == ShutdownMode.OnMainWindowClose,
            "Closing the operator window shuts down Preview and output windows");

        await VerifyCredentialsAndCacheAsync(directory);
        await LiveCanvaRefreshVerification.RunAsync(directory, mainWindow, capture);
        await MirrorImageExportVerification.RunAsync(directory);
        checks.Add("DPAPI reuse, fresh rendered images instead of preview thumbnails, preview API schema guard, immutable refresh and cache save/recovery checks");
        checks.Add("Saved-token startup, concurrent token rotation, 401 retry, live refresh navigation, page-count change, failed export, pause/blank preservation, opt-out and shutdown cancellation");
        checks.Add("Mirror pixels change after refresh with unchanged thumbnails; manual refresh bypasses lagging metadata; hidden-page render mapping, bounded image size, signed URL preservation and invalid/failed image guards");
        checks.Add("Real content timer checks in Live control without repeated unchanged exports; refresh health, download/error screenshots, opt-out, retry recovery and timer shutdown");
        viewModel.CanvaClientId = "verification-client";
        viewModel.SetCanvaClientSecret("synthetic-secret-for-isolated-verification");
        viewModel.SaveCanvaConfigurationCommand.Execute(null);
        Require(viewModel.HasSavedCanvaSecret && !viewModel.ShowSecretInput, "Saved-secret indicator replaces the input after saving");

        foreach ((string foreground, string background) in new[] { ("PrimaryTextBrush", "SurfaceBrush"), ("MutedTextBrush", "SurfaceBrush") })
        {
            SolidColorBrush foregroundBrush = (SolidColorBrush)System.Windows.Application.Current.FindResource(foreground);
            SolidColorBrush backgroundBrush = (SolidColorBrush)System.Windows.Application.Current.FindResource(background);
            Require(ContrastAnalyzer.GetContrastRatio(foregroundBrush.Color.ToString()[3..].Insert(0, "#"), backgroundBrush.Color.ToString()[3..].Insert(0, "#")) >= 4.5,
                $"{foreground} meets 4.5:1 on {background}");
        }
        checks.Add("Primary and secondary operator text contrast");

        mainWindow.SetCompactViewForAutomation(false);
        VerificationWindowSizing.Configure(mainWindow);
        foreach ((int width, int height, double scale) in new[] { (1380, 900, 1d), (1366, 768, 1.25d), (1920, 1080, 1.5d), (1920, 1080, 2d) })
        {
            VerificationWindowSizing.SetSize(mainWindow, width / scale, height / scale);
            foreach (OperatorSection section in Enum.GetValues<OperatorSection>())
            {
                viewModel.ActiveSection = section;
                await mainWindow.Dispatcher.InvokeAsync(() => mainWindow.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                VerificationWindowSizing.CheckSize(mainWindow, width / scale, height / scale);
                capture(mainWindow, Path.Combine(directory, $"operator-{section}-{width}x{height}-{scale:0.00}.png"), scale);
            }
        }
        checks.Add("All operator sections at 100%, 125%, 150% and 200% effective layout sizes and raster DPI");

        viewModel.ActiveSection = OperatorSection.Presentation;
        VerificationWindowSizing.SetSize(mainWindow, 1380, 900);
        await mainWindow.Dispatcher.InvokeAsync(() => mainWindow.UpdateLayout(), DispatcherPriority.ApplicationIdle);
        mainWindow.ScrollContentToEndForAutomation();
        await mainWindow.Dispatcher.InvokeAsync(() => mainWindow.UpdateLayout(), DispatcherPriority.ApplicationIdle);
        capture(mainWindow, Path.Combine(directory, "operator-Presentation-refresh-controls.png"), 1);
        mainWindow.ScrollContentToTopForAutomation();

        viewModel.ActiveSection = OperatorSection.LiveControl;
        Feed(1);
        await mainWindow.Dispatcher.InvokeAsync(() => mainWindow.UpdateLayout(), DispatcherPriority.ApplicationIdle);
        capture(mainWindow, Path.Combine(directory, "operator-LiveControl-single-line-cue.png"), 1);
        VerificationWindowSizing.SetSize(mainWindow, 1380, 900);
        mainWindow.ScrollContentToEndForAutomation();
        await mainWindow.Dispatcher.InvokeAsync(() => mainWindow.UpdateLayout(), DispatcherPriority.ApplicationIdle);
        capture(mainWindow, Path.Combine(directory, "operator-LiveControl-appearance-controls.png"), 1);
        mainWindow.ScrollContentToTopForAutomation();
        mainWindow.SetCompactViewForAutomation(true);
        capture(mainWindow, Path.Combine(directory, "compact-following.png"), 1);
        viewModel.TogglePresenterSyncCommand.Execute(null);
        capture(mainWindow, Path.Combine(directory, "compact-paused.png"), 1);
        mainWindow.SetCompactViewForAutomation(false);
        viewModel.TogglePresenterSyncCommand.Execute(null);

        using OutputWindowViewModel outputViewModel = new(coordinator);
        OutputWindow output = new(outputViewModel);
        Require(output.Icon is not null, "Output window loads the CanvaStage icon");
        Require(outputViewModel.NextLineFontSize == 48
            && outputViewModel.NextLineFontWeight == FontWeights.Bold,
            "Output receives the configured next-line font style");
        output.Show();
        System.Windows.Forms.Screen primaryScreen = System.Windows.Forms.Screen.PrimaryScreen
            ?? throw new InvalidOperationException("Windows verification requires a primary display.");
        output.SetDisplayPlacement(primaryScreen.Bounds);
        output.ShowAndActivate();
        await output.Dispatcher.InvokeAsync(() => output.UpdateLayout(), DispatcherPriority.ApplicationIdle);
        Require(output.GetNativeBounds() == primaryScreen.Bounds, "Native fullscreen placement matches the selected display's physical pixel bounds");
        output.SetPreviewPlacement();
        checks.Add("Per-monitor-DPI fullscreen placement uses exact Windows display bounds");
        VerificationWindowSizing.Configure(output);
        try
        {
            await MonitorTypographyVerification.RunAsync(mainWindow, viewModel, output, coordinator, directory, capture);
            checks.Add("First-frame in-app text fit, restored deferred output fit, matching Current/Next typography across four fonts, independent cue size and optional bold");
            await OutputPageUpdateVerification.RunAsync(mainWindow, viewModel, output, coordinator, directory, capture);
            checks.Add("Separate output drawing pixels change across 40 Presenter pages, manual navigation and resume; pause holds pixels; no forced redraw or fitting repairs in the observation path");
            foreach (MonitorLayout layout in Enum.GetValues<MonitorLayout>())
            {
                coordinator.SetLayout(layout);
                coordinator.SetAppearance(MonitorAppearance.Default with { FontSize = 160, ShowClock = true });
                coordinator.StartOutput();
                coordinator.SelectPage(1);
                foreach (double scale in new[] { 1d, 1.5d, 2d })
                {
                    VerificationWindowSizing.SetSize(output, 1920 / scale, 1080 / scale);
                    await output.Dispatcher.InvokeAsync(() => output.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                    VerificationWindowSizing.CheckSize(output, 1920 / scale, 1080 / scale);
                    output.VerifyTextFit();
                    capture(output, Path.Combine(directory, $"output-{layout}-{scale:0.00}.png"), scale);
                }
            }
            coordinator.SelectPage(2);
            coordinator.SetOutputMode(OutputMode.Mirror);
            VerificationWindowSizing.SetSize(output, 1280, 720);
            await output.Dispatcher.InvokeAsync(() => output.UpdateLayout(), DispatcherPriority.ApplicationIdle);
            VerificationWindowSizing.CheckSize(output, 1280, 720);
            capture(output, Path.Combine(directory, "output-mirror.png"), 1);
        }
        finally { coordinator.StopOutput(); output.Close(); }
        checks.Add("All Monitor layouts with long text at maximum requested font size; measured text fit; Mirror output");
        Require(errors.Errors.Count == 0, "No WPF binding errors: " + string.Join(Environment.NewLine, errors.Errors));
        checks.Add("No WPF binding errors");
        await File.WriteAllTextAsync(Path.Combine(directory, "verification.json"), JsonSerializer.Serialize(new { Passed = true, Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static PresentationDocument CreateDocument()
    {
        string[] lines = Enumerable.Range(1, 9).Select(index => $"Line {index}: a longer confidence display sentence that must remain entirely visible.").ToArray();
        return new("verification-design", "Sunday verification", [
            new(1, "Long text", lines, "#0D0E10", "#75E6C8"),
            new(2, "Welcome", [string.Join(" ", Enumerable.Repeat("A long paragraph should show only a single next-line cue.", 12))], "#0D0E10", "#75E6C8"),
            new(3, "Next", ["Thank you for joining us"], "#0D0E10", "#75E6C8")], DateTimeOffset.UtcNow);
    }

    private static async Task VerifyCredentialsAndCacheAsync(string directory)
    {
        string storage = Path.Combine(directory, "isolated-storage");
        CanvaCredentialsStore store = new(storage);
        CanvaCredentials credentials = new("synthetic-client", "synthetic-secret", "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddHours(1));
        store.Save(credentials);
        CanvaCredentialsStore reopened = new(storage);
        Require(reopened.Load() == credentials, "Protected credentials survive reopening");
        var handler = new SyntheticCanvaHandler();
        using HttpClient client = new(handler);
        CanvaConnectService service = new(reopened, client, Path.Combine(storage, "cache"));
        service.SaveConfiguration(credentials.ClientId, "");
        Require(reopened.Load() == credentials, "Blank input reuses saved secret without clearing tokens");
        PresentationDocument first = await service.LoadAsync("verification-design");
        Require(handler.PageMetadataRequests == 1 && handler.ThumbnailRequests == 0 && handler.ImageExportRequests == 1
            && first.UsesRenderedPageImages && first.Pages[0].FallbackImagePath is not null,
            "Mirror caches a fresh render job without downloading the stale/expired thumbnail");
        Require(handler.ExportRequestBeforePageMetadata, "Page image URLs are fetched after PowerPoint export completes");
        string originalPath = first.Pages[0].FallbackImagePath!;
        PresentationDocument second;
        using (FileStream lockedImage = new(originalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            second = await service.LoadAsync("verification-design");
        }
        Require(second.Pages[0].FallbackImagePath != originalPath && File.Exists(originalPath), "Refresh uses new image paths while old images remain open");
        PresentationCacheStore cache = new(Path.Combine(storage, "cache"));
        cache.Save(second);
        Require(new PresentationCacheStore(Path.Combine(storage, "cache")).Load()?.Pages[0].FallbackImagePath == second.Pages[0].FallbackImagePath,
            "Refreshed cache restores across restart");
        string blockedCacheDirectory = Path.Combine(storage, "blocked-cache-directory");
        File.WriteAllText(blockedCacheDirectory, "This file prevents a cache directory from being created.");
        bool failedSaveWasReported = false;
        try
        {
            new PresentationCacheStore(blockedCacheDirectory).Save(second);
        }
        catch (IOException)
        {
            failedSaveWasReported = true;
        }
        Require(failedSaveWasReported, "Cache save failures reach the Apply action instead of reporting success");
        Require(cache.GetStatus().SizeBytes > 0, "Cache size and timestamp are available");
        Require(cache.RemoveObsoleteGenerations(second) == 1 && !File.Exists(originalPath) && File.Exists(second.Pages[0].FallbackImagePath),
            "Cache cleanup removes an old immutable generation and retains the active generation");
        File.Delete(second.Pages[0].FallbackImagePath!);
        PresentationCacheStore damagedCache = new(Path.Combine(storage, "cache"));
        PresentationDocument? recovered = damagedCache.Load();
        Require(recovered is { Pages.Count: 1 } && recovered.Pages[0].FallbackImagePath is null
            && recovered.Pages[0].PrimaryText == second.Pages[0].PrimaryText && damagedCache.MissingImageCountOnLoad == 1,
            "Missing cached image is removed from restored deck while native text remains available");

        using HttpClient changedApiClient = new(new SyntheticCanvaHandler(omitPageNumber: true));
        CanvaConnectService changedApi = new(reopened, changedApiClient, Path.Combine(storage, "changed-api-cache"));
        try
        {
            _ = await changedApi.LoadAsync("verification-design");
            throw new InvalidOperationException("Windows verification failed: changed Canva page schema was accepted");
        }
        catch (CanvaConnectException exception) when (exception.Message.Contains("page numbers", StringComparison.Ordinal))
        {
            // A preview API schema change must fail safely instead of pairing the wrong images with pages.
        }
    }

    private sealed class SyntheticCanvaHandler : HttpMessageHandler
    {
        private readonly bool _omitPageNumber;
        public SyntheticCanvaHandler(bool omitPageNumber = false) => _omitPageNumber = omitPageNumber;
        public int PageMetadataRequests { get; private set; }
        public int ThumbnailRequests { get; private set; }
        public int ImageExportRequests { get; private set; }
        public bool ExportRequestBeforePageMetadata { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/pages", StringComparison.Ordinal)) PageMetadataRequests++;
            if (path.EndsWith("/exports", StringComparison.Ordinal) && PageMetadataRequests == 0) ExportRequestBeforePageMetadata = true;
            if (path.EndsWith(".png", StringComparison.Ordinal)) ThumbnailRequests++;
            bool isImageExport = false;
            if (path.EndsWith("/exports", StringComparison.Ordinal))
            {
                using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                isImageExport = body.RootElement.GetProperty("format").GetProperty("type").GetString() == "jpg";
                if (isImageExport) ImageExportRequests++;
            }
            HttpContent content;
            if (path.EndsWith(".pptx", StringComparison.Ordinal))
            {
                using MemoryStream stream = new();
                using (ZipArchive zip = new(stream, ZipArchiveMode.Create, true))
                {
                    using StreamWriter writer = new(zip.CreateEntry("ppt/slides/slide1.xml").Open());
                    writer.Write("<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><a:p><a:r><a:t>Welcome</a:t></a:r></a:p></p:sld>");
                }
                content = new ByteArrayContent(stream.ToArray());
            }
            else if (path.EndsWith(".jpg", StringComparison.Ordinal))
                content = new ByteArrayContent(CreateVerificationImage(80));
            else
            {
                string json = path.EndsWith("/pages", StringComparison.Ordinal)
                    ? $"{{\"items\":[{{{(_omitPageNumber ? string.Empty : "\"page_number\":1,")}\"id\":\"page1\",\"thumbnail\":{{\"url\":\"https://test.invalid/{(PageMetadataRequests == 1 ? "expired" : "page")}.png\"}}}}]}}"
                    : path.EndsWith("/exports", StringComparison.Ordinal)
                        ? $"{{\"job\":{{\"id\":\"export1\",\"status\":\"success\",\"urls\":[\"https://test.invalid/{(isImageExport ? "rendered.jpg" : "deck.pptx")}\"]}}}}"
                        : "{\"design\":{\"id\":\"verification-design\",\"title\":\"Sunday verification\",\"design_types\":[\"presentation\"],\"page_count\":1,\"updated_at\":1893456000}}";
                content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    internal static byte[] CreateVerificationImage(byte red)
    {
        byte[] pixels = [0, 0, red, 255];
        System.Windows.Media.Imaging.BitmapSource image = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96,
            PixelFormats.Bgra32, null, pixels, 4);
        System.Windows.Media.Imaging.PngBitmapEncoder encoder = new();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using MemoryStream stream = new();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private sealed class BindingErrors : TraceListener, IDisposable
    {
        public List<string> Errors { get; } = [];
        private readonly SourceLevels _previousLevel = PresentationTraceSources.DataBindingSource.Switch.Level;
        public BindingErrors()
        {
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            PresentationTraceSources.DataBindingSource.Listeners.Add(this);
        }
        public override void Write(string? message) { if (!string.IsNullOrWhiteSpace(message)) Errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
        protected override void Dispose(bool disposing)
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(this);
            PresentationTraceSources.DataBindingSource.Switch.Level = _previousLevel;
            base.Dispose(disposing);
        }
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Windows verification failed: " + description);
    }
}
