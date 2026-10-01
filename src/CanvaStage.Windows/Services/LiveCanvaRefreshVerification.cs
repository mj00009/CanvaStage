using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Windows.ViewModels;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CanvaStage.Windows.Services;

/// <summary>Exercises real service/view-model refresh paths with delayed synthetic Canva responses.</summary>
internal static class LiveCanvaRefreshVerification
{
    public static async Task RunAsync(string directory)
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
            Require(output.Snapshot.Document.Id == "live-deck" && output.PrimaryText == "Version 6 page 2"
                && handler.ExportRequests == exports + 2,
                "Re-download forces fresh exports of the applied deck, not the highlighted dropdown entry");
            viewModel.IsAutomaticContentRefreshEnabled = true;
            working = output.Snapshot.Document;
            handler.Version++;
            handler.BlockDownload();
            refresh = viewModel.CheckAutomaticContentRefreshAsync(true);
            await handler.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
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
                        writer.Write($"<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><a:p><a:r><a:t>Version {Version} page {page}</a:t></a:r></a:p></p:sld>");
                    }
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(stream.ToArray()) };
            }
            if (path.EndsWith("/pages", StringComparison.Ordinal))
                return Json("{\"items\":[" + string.Join(",", Enumerable.Range(1, PageCount).Select(page => $"{{\"page_number\":{page},\"id\":\"page-{page}\",\"thumbnail\":{{\"url\":\"https://test.invalid/stale.png\"}}}}")) + "]}");
            string design = $"{{\"id\":\"live-deck\",\"title\":\"Live deck\",\"design_types\":[\"presentation\"],\"page_count\":{PageCount},\"updated_at\":{1893456000 + (MetadataVersionOverride ?? Version)}}}";
            return Json(path.EndsWith("/designs", StringComparison.Ordinal) ? $"{{\"items\":[{design}]}}" : $"{{\"design\":{design}}}");
        }

        private static HttpResponseMessage Json(string content, HttpStatusCode status = HttpStatusCode.OK)
            => new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    }
}
