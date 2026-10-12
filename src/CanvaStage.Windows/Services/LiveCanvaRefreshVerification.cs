using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CanvaStage.Windows.Services;

/// <summary>Exercises real service/view-model refresh paths with delayed synthetic Canva responses.</summary>
internal static class LiveCanvaRefreshVerification
{
    public static async Task RunAsync(string directory, MainWindow mainWindow, Action<Window, string, double> capture)
    {
        string storage = Path.Combine(directory, "isolated-storage", "live-refresh");
        CanvaCredentialsStore store = new(storage);
        CanvaCredentials credentials = new("synthetic-client", "synthetic-secret", "expired-access", "refresh-0", DateTimeOffset.UtcNow.AddHours(-1));
        store.Save(credentials);
        LiveCanvaHandler handler = new();
        using HttpClient client = new(handler);
        CanvaConnectService service = new(store, client, Path.Combine(storage, "cache"));

        await Task.WhenAll(service.GetPresentationAsync("live-deck"), service.GetPresentationAsync("live-deck"));
        Require(handler.TokenRequests == 1, "Concurrent expired-token requests share one refresh-token rotation");
        Require(new CanvaCredentialsStore(storage).Load()?.RefreshToken == "refresh-1", "Rotated authorization survives restart");
        store.Save(store.Load()! with { AccessToken = "rejected-access" });
        await service.GetPresentationAsync("live-deck");
        Require(handler.TokenRequests == 2, "A rejected access token is refreshed and retried without browser authorization");
        await service.ConnectAsync();
        Require(handler.TokenRequests == 2, "Connect reuses existing valid authorization");

        PresentationDocument initial = await service.LoadAsync("live-deck");
        PresentationCacheStore cache = new(Path.Combine(storage, "cache"));
        cache.Save(initial);
        AppPreferences preferences = AppPreferences.Default with { IsAutomaticContentRefreshEnabled = false };
        OutputCoordinator coordinator = new(initial, preferences);
        OutputWindowManager manager = new(coordinator);
        DisplayService displays = new();
        MainViewModel viewModel = new(coordinator, manager, displays, new DisplayIdentificationService(displays),
            new DiagnosticsExportService(), new JsonPreferencesStore(storage), cache, service,
            new WindowsReleaseVerification.DemoTracker(), preferences);
        using OutputWindowViewModel output = new(coordinator);
        try
        {
            await viewModel.InitializeCanvaAsync();
            Require(viewModel.CanvaPresentations.Count == 1 && viewModel.SelectedCanvaPresentation?.Id == "live-deck",
                "Startup loads presentation choices using saved authorization");
            coordinator.SetDocument(initial with { UsesRenderedPageImages = false });
            viewModel.IsAutomaticContentRefreshEnabled = true;
            await viewModel.CheckAutomaticContentRefreshAsync(true);
            initial = coordinator.Snapshot.Document;
            Require(initial.UsesRenderedPageImages && cache.Load()?.UsesRenderedPageImages == true,
                "A legacy thumbnail cache migrates once even when its Canva edit timestamp has not changed");
            void Feed(int page) => viewModel.ReceivePresenterUpdateForVerification(new(
                CanvaPresenterTrackingState.PageDetected, page, handler.PageCount, "Synthetic Presenter", DateTimeOffset.UtcNow));
            coordinator.StartOutput();
            Feed(2);
            coordinator.SetOutputMode(OutputMode.Mirror);
            byte oldPixel = ReadRed(output.FallbackImage);
            handler.Version++;
            handler.BlockDownload();
            Task refresh = viewModel.CheckAutomaticContentRefreshAsync(true);
            await handler.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Feed(3);
            coordinator.SetBlanked(true);
            Require(output.Snapshot.SelectedPageNumber == 3 && ReferenceEquals(output.Snapshot.Document, initial),
                "Presenter updates reach output while the refresh download is delayed");
            int exports = handler.ExportRequests;
            await viewModel.CheckAutomaticContentRefreshAsync(true);
            Require(handler.ExportRequests == exports, "A concurrent content check does not start a duplicate export");
            handler.ReleaseDownload.TrySetResult();
            await refresh;
            Require(output.Snapshot is { IsLive: true, IsBlanked: true, SelectedPageNumber: 3 }
                && output.PrimaryText == "Version 2 page 3" && viewModel.PresenterSyncSummary == "Following Canva",
                "Live refresh retains the latest Presenter page and blank state in both views");
            Require(oldPixel != ReadRed(output.FallbackImage) && ReadRed(output.FallbackImage) == 60
                && output.IsMirror && handler.ThumbnailRequests == 0,
                "Mirror replaces its decoded image after live refresh even when preview thumbnails never change");
            Feed(1);
            Require(output.Snapshot.SelectedPageNumber == 1 && output.PrimaryText == "Version 2 page 1",
                "Output continues following after deck replacement");

            handler.PageCount = 4;
            handler.Version++;
            Feed(4);
            Require(viewModel.PresenterSyncSummary == "Presentation mismatch", "A changed page count safely holds the old deck");
            await viewModel.CheckAutomaticContentRefreshAsync(true);
            Require(output.Snapshot.SelectedPageNumber == 4 && output.PrimaryText == "Version 3 page 4"
                && viewModel.PresenterSyncSummary == "Following Canva", "Refresh resolves a changed page count without restarting sync");

            PresentationDocument working = output.Snapshot.Document;
            handler.Version++;
            handler.FailExport = true;
            await viewModel.CheckAutomaticContentRefreshAsync(true);
            Feed(2);
            Require(ReferenceEquals(output.Snapshot.Document, working) && output.Snapshot.SelectedPageNumber == 2
                && !viewModel.IsCanvaBusy, "A failed refresh retains usable live output and releases the busy state");
            Require(viewModel.ContentRefreshHealthText.Contains("Needs attention", StringComparison.Ordinal)
                && viewModel.ContentRefreshTimingText.Contains("Retry:", StringComparison.Ordinal)
                && viewModel.ContentRefreshDetail.Contains("remains available", StringComparison.Ordinal),
                "Live control reports refresh failure, retained content and scheduled retry");
            await CaptureHealthAsync("failed-retry");
            handler.FailExport = false;
            viewModel.PreviousPageCommand.Execute(null);
            await viewModel.CheckAutomaticContentRefreshAsync(true);
            Require(viewModel.PresenterSyncSummary.StartsWith("Paused", StringComparison.Ordinal)
                && output.Snapshot.SelectedPageNumber == 1, "Refresh preserves a manual navigation pause");
            viewModel.TogglePresenterSyncCommand.Execute(null);
            Require(output.Snapshot.SelectedPageNumber == 2, "Resume after refresh uses the latest Presenter counter");

            handler.MetadataVersionOverride = handler.Version;
            handler.Version++;
            oldPixel = ReadRed(output.FallbackImage);
            await viewModel.CheckPresentationUpdatesAsync();
            Require(ReadRed(output.FallbackImage) != oldPixel && output.PrimaryText == "Version 5 page 2",
                "Manual update forces fresh text and Mirror pixels even if Canva modification metadata lags");
            handler.MetadataVersionOverride = null;

            PresentationDocument beforeImageFailure = output.Snapshot.Document;
            handler.Version++;
            handler.FailImageExport = true;
            await viewModel.CheckAutomaticContentRefreshAsync(true);
            Require(ReferenceEquals(output.Snapshot.Document, beforeImageFailure) && !viewModel.IsCanvaBusy,
                "A failed fresh image export keeps the entire working deck instead of silently using stale thumbnails");
            handler.FailImageExport = false;

            viewModel.IsAutomaticContentRefreshEnabled = false;
            exports = handler.ExportRequests;
            await viewModel.CheckPresentationUpdatesAsync();
            Require(handler.ExportRequests == exports && ReferenceEquals(output.Snapshot.Document, beforeImageFailure)
                && viewModel.IsPresentationUpdateAvailable,
                "Manual Check for updates remains metadata-only when automatic updates are disabled");
            viewModel.SelectedCanvaPresentation = new("other-deck", "Other deck", 1);
            await viewModel.RedownloadCanvaAsync();
            Require(!viewModel.ContentRefreshHealthText.Contains("Needs attention", StringComparison.Ordinal),
                "A successful manual re-download clears the previous refresh error");
            Require(output.Snapshot.Document.Id == "live-deck" && output.PrimaryText == "Version 6 page 2"
                && handler.ExportRequests == exports + 2,
                "Re-download forces fresh exports of the applied deck, not the highlighted dropdown entry");
            viewModel.IsAutomaticContentRefreshEnabled = true;
            working = output.Snapshot.Document;
            handler.Version++;
            handler.BlockDownload();
            refresh = viewModel.CheckAutomaticContentRefreshAsync(true);
            await handler.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Require(viewModel.IsCanvaDownloadProgressVisible
                && viewModel.ContentRefreshHealthText.Contains("Downloading", StringComparison.Ordinal),
                "Live control exposes full-deck download progress while presenting");
            await CaptureHealthAsync("downloading");
            viewModel.IsAutomaticContentRefreshEnabled = false;
            handler.ReleaseDownload.TrySetResult();
            await refresh;
            Require(ReferenceEquals(output.Snapshot.Document, working) && cache.Load()?.SourceModifiedAt == working.SourceModifiedAt,
                "Turning automatic refresh off during a download prevents a late deck/cache replacement");

            viewModel.IsAutomaticContentRefreshEnabled = true;
            handler.BlockDownload();
            refresh = viewModel.CheckAutomaticContentRefreshAsync(true);
            await handler.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            viewModel.PrepareToClose();
            await refresh;
            Require(ReferenceEquals(coordinator.Snapshot.Document, working), "Closing cancels an in-progress refresh before it can replace content");
        }
        finally { viewModel.PrepareToClose(); }

        await VerifySimpleSetupAsync();
        await VerifyScheduledChecksAsync();
        handler.RejectRefreshToken = true;
        store.Save(store.Load()! with { AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) });
        try
        {
            await service.GetPresentationAsync("live-deck");
            throw new InvalidOperationException("Revoked refresh token was accepted.");
        }
        catch (CanvaAuthorizationRequiredException)
        {
            Require(store.Load() is { RefreshToken: null, ClientSecret: "synthetic-secret" },
                "Revoked authorization reports reconnect while preserving protected setup");
        }

        async Task CaptureHealthAsync(string state, MainViewModel? target = null)
        {
            object previousContext = mainWindow.DataContext;
            try
            {
                mainWindow.DataContext = target ?? viewModel;
                // Startup screenshot capture leaves compact view active. Health belongs
                // to full Live control, so make that surface visible before checking it.
                mainWindow.SetCompactViewForAutomation(false);
                VerificationWindowSizing.Configure(mainWindow);
                VerificationWindowSizing.SetSize(mainWindow, 1380, 900);
                mainWindow.ScrollContentToTopForAutomation();
                await mainWindow.Dispatcher.InvokeAsync(() => mainWindow.UpdateLayout(),
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                FrameworkElement card = (FrameworkElement)mainWindow.FindName("ContentRefreshHealthCard");
                Require(card.IsVisible && card.ActualHeight > 0, "Refresh health is visible without opening Presentation");
                capture(mainWindow, Path.Combine(directory, $"operator-LiveControl-refresh-{state}.png"), 1);
            }
            finally { mainWindow.DataContext = previousContext; }
        }

        async Task VerifyScheduledChecksAsync()
        {
            PresentationDocument current = await service.LoadAsync("live-deck");
            OutputCoordinator scheduledCoordinator = new(current, preferences);
            MainViewModel scheduled = new(scheduledCoordinator, new OutputWindowManager(scheduledCoordinator), displays,
                new DisplayIdentificationService(displays), new DiagnosticsExportService(), new JsonPreferencesStore(storage),
                cache, service, new WindowsReleaseVerification.DemoTracker(), preferences,
                new AutomaticContentRefreshSchedule(interval: TimeSpan.FromMilliseconds(150)));
            try
            {
                int exports = handler.ExportRequests;
                int checks = handler.MetadataRequests;
                scheduled.IsAutomaticContentRefreshEnabled = true;
                await WaitUntilAsync(() => handler.MetadataRequests >= checks + 3 && !scheduled.IsCanvaBusy);
                Require(handler.ExportRequests == exports && scheduled.ActiveSection == OperatorSection.LiveControl,
                    "Real timer ticks check unchanged metadata repeatedly in Live control without exporting the deck");
                Require(scheduled.ContentRefreshHealthText.EndsWith("Checked", StringComparison.Ordinal)
                    && !scheduled.ContentRefreshTimingText.Contains("Last check: not yet", StringComparison.Ordinal),
                    "Live refresh health records completed timer-driven checks");
                await CaptureHealthAsync("checked", scheduled);

                scheduled.IsAutomaticContentRefreshEnabled = false;
                checks = handler.MetadataRequests;
                await Task.Delay(400);
                Require(handler.MetadataRequests == checks && scheduled.ContentRefreshHealthText.Contains("off", StringComparison.Ordinal),
                    "Disabling automatic updates stops timer-driven requests and reports the disabled state");

                handler.FailMetadata = true;
                scheduled.IsAutomaticContentRefreshEnabled = true;
                await WaitUntilAsync(() => scheduled.ContentRefreshHealthText.Contains("Needs attention", StringComparison.Ordinal));
                Require(scheduled.ContentRefreshTimingText.Contains("Retry:", StringComparison.Ordinal),
                    "A timer-driven failure advertises retry timing");
                handler.FailMetadata = false;
                await WaitUntilAsync(() => scheduled.ContentRefreshHealthText.EndsWith("Checked", StringComparison.Ordinal));
                Require(handler.ExportRequests == exports, "A scheduled retry recovers without downloading unchanged content");

                scheduledCoordinator.StartOutput();
                handler.Version++;
                handler.BlockDownload();
                await handler.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                checks = handler.MetadataRequests;
                scheduled.ReceivePresenterUpdateForVerification(new(CanvaPresenterTrackingState.PageDetected,
                    3, handler.PageCount, "Synthetic Presenter", DateTimeOffset.UtcNow));
                scheduledCoordinator.SetBlanked(true);
                await Task.Delay(350);
                Require(handler.MetadataRequests == checks && scheduled.IsCanvaBusy,
                    "Scheduled checks cannot overlap a full-deck download");
                handler.ReleaseDownload.TrySetResult();
                await WaitUntilAsync(() => !scheduled.IsCanvaBusy);
                Require(scheduledCoordinator.Snapshot is { IsLive: true, IsBlanked: true, SelectedPageNumber: 3 }
                    && scheduledCoordinator.Snapshot.Document.Pages[2].PrimaryText == $"Version {handler.Version} page 3",
                    "A timer-driven changed deck replaces live content without restarting output or following");
                scheduled.PrepareToClose();
                checks = handler.MetadataRequests;
                await Task.Delay(400);
                Require(handler.MetadataRequests == checks, "Closing disarms the content timer");
            }
            finally
            {
                handler.FailMetadata = false;
                scheduled.PrepareToClose();
            }
        }

        async Task VerifySimpleSetupAsync()
        {
            // Exercise the real view-model/service path. The runner has one actual
            // monitor; an injected list treats that physical target as an Extended
            // test adapter. This is not proof of multi-display hardware behavior.
            DisplayOption physical = displays.GetDisplays()[0];
            DisplayOption target = physical with { IsPrimary = false };
            IReadOnlyList<DisplayOption> available = [target];
            PresentationDocument current = await service.LoadAsync("live-deck");
            AppPreferences simplePreferences = preferences with { SelectedDisplayId = target.Id };
            OutputCoordinator simpleCoordinator = new(current, simplePreferences);
            OutputWindowManager simpleManager = new(simpleCoordinator);
            JsonPreferencesStore simpleStore = new(Path.Combine(storage, "simple-setup"));
            MainViewModel simple = new(simpleCoordinator, simpleManager, displays, new DisplayIdentificationService(displays),
                new DiagnosticsExportService(), simpleStore, cache, service, new WindowsReleaseVerification.DemoTracker(),
                simplePreferences, getDisplays: () => available);
            object previousContext = mainWindow.DataContext;
            try
            {
                mainWindow.DataContext = simple;
                simple.ActiveSection = OperatorSection.Setup;
                mainWindow.SetCompactViewForAutomation(false);
                mainWindow.ScrollContentToTopForAutomation();
                Require(simple.IsSimpleSetup && !simple.IsAdvancedSetup, "Simple Setup is the initial tab");
                int exports = handler.ExportRequests;
                await simple.StartSimpleSetupAsync();
                Require(simpleCoordinator.Snapshot.IsLive && simpleManager.WindowForVerification.IsVisible
                    && handler.ExportRequests == exports && simple.SimpleSetupStatus.Contains("No changes"),
                    "Simple Setup cannot reuse unchanged intact cache and start the saved target without exports");
                Require(simpleStore.Load().LastAppliedPresentationId == "live-deck", "Simple Setup does not remember the applied deck");
                await mainWindow.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                capture(mainWindow, Path.Combine(directory, "setup-simple-cached-ready.png"), 1);
                simple.StopOutputCommand.Execute(null);
                Require(!simpleCoordinator.Snapshot.IsLive && !simpleManager.WindowForVerification.IsVisible,
                    "Stop after Simple Setup does not stop live output");
                await simple.CheckSimpleSlidesAsync();
                Require(handler.ExportRequests == exports && !simpleCoordinator.Snapshot.IsLive,
                    "Simple Check slides downloads unchanged content or starts output");

                available = [];
                await simple.StartSimpleSetupAsync();
                Require(!simpleCoordinator.Snapshot.IsLive && handler.ExportRequests == exports
                    && simple.SimpleSetupStatus.Contains("Extend"), "Missing saved display starts an arbitrary target");
                available = [physical];
                await simple.StartSimpleSetupAsync();
                Require(!simpleCoordinator.Snapshot.IsLive && simple.SimpleSetupStatus.Contains("primary screen"),
                    "Simple Setup starts output on Primary without an explicit Advanced action");
                available = [target];

                // Highlighting a different entry must not change the last-used default.
                simple.SelectedCanvaPresentation = new("other-deck", "Not applied", 1);
                await simple.StartSimpleSetupAsync();
                Require(simple.SelectedCanvaPresentation?.Id == "live-deck" && handler.ExportRequests == exports,
                    "Simple Setup uses a highlighted but never-applied presentation");
                simple.StopOutputCommand.Execute(null);

                handler.Version++;
                handler.BlockDownload();
                Task setup = simple.StartSimpleSetupAsync();
                await handler.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await mainWindow.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Require(simple.IsSimpleSetupRunning && simple.IsCanvaDownloadProgressVisible
                    && simple.SimpleSetupStatus.Contains("3 of 4") && simple.StopOutputCommand.CanExecute(null),
                    "Simple Setup does not expose download stage/progress or cancellation");
                capture(mainWindow, Path.Combine(directory, "setup-simple-downloading.png"), 1);
                simple.StopOutputCommand.Execute(null);
                handler.ReleaseDownload.TrySetResult();
                await setup;
                Require(!simpleCoordinator.Snapshot.IsLive && !simpleManager.WindowForVerification.IsVisible
                    && simple.SimpleSetupStatus.Contains("cancelled"), "Late Simple Setup completion restarts cancelled output");

                await simple.StartSimpleSetupAsync();
                Require(simpleCoordinator.Snapshot.IsLive && simple.SimpleSetupStatus.Contains("No changes"),
                    "Restart setup cannot use the newly cached deck after cancelling output start");
                simple.StopOutputCommand.Execute(null);
                PresentationDocument working = simpleCoordinator.Snapshot.Document;
                handler.Version++;
                handler.FailExport = true;
                await simple.StartSimpleSetupAsync();
                Require(!simpleCoordinator.Snapshot.IsLive && !simple.IsCanvaBusy && !simple.IsSimpleSetupRunning && simple.SimpleSetupProgress == 0
                    && ReferenceEquals(working, simpleCoordinator.Snapshot.Document), "Failed Simple Setup discards the cached deck or leaves busy state stuck");
                capture(mainWindow, Path.Combine(directory, "setup-simple-export-failure.png"), 1);
            }
            finally
            {
                handler.FailExport = false;
                handler.ReleaseDownload.TrySetResult();
                simple.PrepareToClose();
                mainWindow.DataContext = previousContext;
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Windows live-refresh verification failed: " + message);
    }

    private static byte ReadRed(BitmapSource? image)
    {
        if (image is null) throw new InvalidOperationException("Mirror verification did not load an image.");
        FormatConvertedBitmap converted = new(image, PixelFormats.Bgra32, null, 0);
        byte[] pixels = new byte[4];
        converted.CopyPixels(pixels, 4, 0);
        return pixels[2];
    }

    private sealed class LiveCanvaHandler : HttpMessageHandler
    {
        public int Version { get; set; } = 1;
        public int PageCount { get; set; } = 3;
        public int TokenRequests { get; private set; }
        public int ExportRequests { get; private set; }
        public int MetadataRequests { get; private set; }
        public bool FailMetadata { get; set; }
        public bool FailExport { get; set; }
        public bool FailImageExport { get; set; }
        public int? MetadataVersionOverride { get; set; }
        public int ThumbnailRequests { get; private set; }
        public bool RejectRefreshToken { get; set; }
        public TaskCompletionSource DownloadStarted { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDownload { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _blockDownload;

        public void BlockDownload()
        {
            DownloadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            ReleaseDownload = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _blockDownload = true;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("oauth/token", StringComparison.Ordinal))
            {
                TokenRequests++;
                await Task.Delay(30, cancellationToken).ConfigureAwait(false);
                if (RejectRefreshToken) return Json("{\"error\":\"invalid_grant\"}", HttpStatusCode.BadRequest);
                return Json($"{{\"access_token\":\"access-{TokenRequests}\",\"refresh_token\":\"refresh-{TokenRequests}\",\"expires_in\":3600}}");
            }
            if (request.RequestUri.Host == "api.canva.com" && request.Headers.Authorization?.Parameter != $"access-{TokenRequests}")
                return Json("{\"code\":\"invalid_access_token\"}", HttpStatusCode.Unauthorized);
            if (path.EndsWith("/exports", StringComparison.Ordinal))
            {
                ExportRequests++;
                if (FailExport) return Json("{}", HttpStatusCode.InternalServerError);
                using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                if (body.RootElement.GetProperty("format").GetProperty("type").GetString() == "jpg")
                {
                    if (FailImageExport) return Json("{\"job\":{\"id\":\"image-export\",\"status\":\"failed\"}}");
                    string urls = string.Join(",", body.RootElement.GetProperty("format").GetProperty("pages").EnumerateArray()
                        .Select(page => $"\"https://test.invalid/image/{Version}/{page.GetInt32()}.jpg\""));
                    return Json($"{{\"job\":{{\"id\":\"image-export\",\"status\":\"success\",\"urls\":[{urls}]}}}}");
                }
                return Json("{\"job\":{\"id\":\"export\",\"status\":\"success\",\"urls\":[\"https://test.invalid/deck.pptx\"]}}");
            }
            if (path.EndsWith(".jpg", StringComparison.Ordinal))
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(WindowsReleaseVerification.CreateVerificationImage((byte)(int.Parse(path.Split('/')[2]) * 30))) };
            if (path.EndsWith("stale.png", StringComparison.Ordinal))
            {
                ThumbnailRequests++;
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(WindowsReleaseVerification.CreateVerificationImage(30)) };
            }
            if (path.EndsWith(".pptx", StringComparison.Ordinal))
            {
                if (_blockDownload)
                {
                    _blockDownload = false;
                    DownloadStarted.TrySetResult();
                    await ReleaseDownload.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                using MemoryStream stream = new();
                using (ZipArchive zip = new(stream, ZipArchiveMode.Create, true))
                    for (int page = 1; page <= PageCount; page++)
                    {
                        using StreamWriter writer = new(zip.CreateEntry($"ppt/slides/slide{page}.xml").Open());
                        writer.Write($"<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><a:p><a:r><a:t>Version</a:t></a:r><a:r><a:t> </a:t></a:r><a:r><a:t>{Version}</a:t></a:r><a:r><a:t> </a:t></a:r><a:r><a:t>page</a:t></a:r><a:r><a:t> </a:t></a:r><a:r><a:t>{page}</a:t></a:r></a:p></p:sld>");
                    }
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(stream.ToArray()) };
            }
            if (path.EndsWith("/pages", StringComparison.Ordinal))
                return Json("{\"items\":[" + string.Join(",", Enumerable.Range(1, PageCount).Select(page => $"{{\"page_number\":{page},\"id\":\"page-{page}\",\"thumbnail\":{{\"url\":\"https://test.invalid/stale.png\"}}}}")) + "]}");
            if (path.EndsWith("/designs/live-deck", StringComparison.Ordinal))
            {
                MetadataRequests++;
                await Task.Delay(30, cancellationToken).ConfigureAwait(false);
                if (FailMetadata) return Json("{}", HttpStatusCode.InternalServerError);
            }
            string design = $"{{\"id\":\"live-deck\",\"title\":\"Live deck\",\"design_types\":[\"presentation\"],\"page_count\":{PageCount},\"updated_at\":{1893456000 + (MetadataVersionOverride ?? Version)}}}";
            return Json(path.EndsWith("/designs", StringComparison.Ordinal) ? $"{{\"items\":[{design}]}}" : $"{{\"design\":{design}}}");
        }

        private static HttpResponseMessage Json(string content, HttpStatusCode status = HttpStatusCode.OK)
            => new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    }
}
