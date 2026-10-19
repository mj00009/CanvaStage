using CanvaStage.Core.Models;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace CanvaStage.Windows.Services;

/// <summary>Verifies fresh image export mapping and safe failure without a live Canva account.</summary>
internal static class MirrorImageExportVerification
{
    public static async Task RunAsync(string directory)
    {
        string storage = Path.Combine(directory, "isolated-storage", "mirror-export");
        CanvaCredentialsStore credentials = new(storage);
        credentials.Save(new("test-client", "test-secret", "test-access", "test-refresh", DateTimeOffset.UtcNow.AddHours(1)));
        MirrorHandler handler = new();
        using HttpClient client = new(handler);
        CanvaConnectService service = new(credentials, client, Path.Combine(storage, "cache"));
        PresentationDocument document = await service.LoadAsync("hidden-deck");
        Require(document.Pages.Count == 2 && document.Pages[0].ExternalPageId == "source-1"
            && document.Pages[1].ExternalPageId == "source-3" && handler.RequestedPages.SequenceEqual([1, 3]),
            "Fresh image exports use visible source page numbers and preserve hidden-slide mapping");
        Require(handler.RequestedWidth == 1280 && handler.RequestedQuality == 70 && handler.RequestedExportQuality == "regular",
            "Reference renders are bounded, compressed, and use regular export quality");
        Require(File.ReadAllBytes(document.Pages[0].FallbackImagePath!).SequenceEqual(WindowsReleaseVerification.CreateVerificationImage(40))
            && File.ReadAllBytes(document.Pages[1].FallbackImagePath!).SequenceEqual(WindowsReleaseVerification.CreateVerificationImage(120)),
            "Per-page render URLs are paired in requested page order");
        handler.PageWidth = 800;
        await service.LoadAsync("hidden-deck");
        Require(handler.RequestedWidth == 800, "Small designs are not upscaled beyond their original width");

        handler.WrongImageCount = true;
        await ExpectFailureAsync(service, "number of rendered page images");
        handler.WrongImageCount = false;
        handler.InvalidImage = true;
        await ExpectFailureAsync(service, "new image for page");
        handler.FailImageDownload = false;
        handler.InvalidImage = false;
        handler.CapitalizationSample = true;
        int reads = 0;
        string? reference = "Everyone should read this sample text with God and the USA.";
        bool failRecognition = false;
        CanvaConnectService matching = new(credentials, client, Path.Combine(storage, "case-cache"), (_, token) =>
        {
            token.ThrowIfCancellationRequested(); reads++;
            if (failRecognition) throw new IOException("Synthetic recognition failure");
            return Task.FromResult<string?>(reference);
        });
        await matching.LoadAsync("hidden-deck");
        Require(reads == 0, "Default import must not invoke OCR");
        PresentationDocument matched = await matching.LoadAsync("hidden-deck", true);
        Require(reads == 2 && matched.CapitalizationAdjustedPages == 2 && matched.Pages[0].PrimaryText == reference
            && matched.Pages[0].TextBlocks[0].StartsWith("EVERYONE SHOULD"), "Case matching must retain original text and visible-page mapping");
        reference = "Different wording is not casing evidence";
        Require((await matching.LoadAsync("hidden-deck", true)).CapitalizationUnchangedPages == 2, "Uncertain OCR must keep exported text");
        reference = null;
        Require((await matching.LoadAsync("hidden-deck", true)).CapitalizationUnavailablePages == 2, "Missing OCR must be reported without failing import");
        failRecognition = true;
        Require((await matching.LoadAsync("hidden-deck", true)).Pages[0].PrimaryText.StartsWith("EVERYONE SHOULD"), "OCR failure must retain native content");
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        bool cancelledImport = false;
        try { await matching.LoadAsync("hidden-deck", true, cancelled.Token); }
        catch (OperationCanceledException) { cancelledImport = true; }
        Require(cancelledImport, "Cancelled case import must not complete");
        handler.InvalidImage = false;
        handler.FailImageDownload = true;
        await ExpectFailureAsync(service, "new image for page");
    }

    private static async Task ExpectFailureAsync(CanvaConnectService service, string message)
    {
        try
        {
            await service.LoadAsync("hidden-deck");
            throw new InvalidOperationException("Mirror export verification accepted invalid render data.");
        }
        catch (CanvaConnectException exception) when (exception.Message.Contains(message, StringComparison.Ordinal)) { }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Mirror export verification failed: " + message);
    }

    private sealed class MirrorHandler : HttpMessageHandler
    {
        public int[] RequestedPages { get; private set; } = [];
        public int RequestedWidth { get; private set; }
        public int RequestedQuality { get; private set; }
        public string? RequestedExportQuality { get; private set; }
        public int PageWidth { get; set; } = 1920;
        public bool WrongImageCount { get; set; }
        public bool InvalidImage { get; set; }
        public bool FailImageDownload { get; set; }
        public bool CapitalizationSample { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && request.Headers.CacheControl?.NoCache != true)
                throw new InvalidOperationException("Canva content reads should request revalidation without changing signed URLs.");
            if (path.EndsWith("/exports", StringComparison.Ordinal))
            {
                using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                JsonElement format = body.RootElement.GetProperty("format");
                if (format.GetProperty("type").GetString() == "pptx")
                    return Json("{\"job\":{\"id\":\"pptx\",\"status\":\"success\",\"urls\":[\"https://test.invalid/deck.pptx\"]}}");
                RequestedPages = format.GetProperty("pages").EnumerateArray().Select(page => page.GetInt32()).ToArray();
                RequestedWidth = format.GetProperty("width").GetInt32();
                RequestedQuality = format.GetProperty("quality").GetInt32();
                RequestedExportQuality = format.GetProperty("export_quality").GetString();
                string urls = string.Join(",", RequestedPages.Take(WrongImageCount ? 1 : RequestedPages.Length)
                    .Select(page => $"\"https://test.invalid/{page}.jpg?signature=unchanged\""));
                return Json($"{{\"job\":{{\"id\":\"jpg\",\"status\":\"success\",\"urls\":[{urls}]}}}}");
            }
            if (path.EndsWith(".pptx", StringComparison.Ordinal))
            {
                using MemoryStream stream = new();
                using (ZipArchive archive = new(stream, ZipArchiveMode.Create, true))
                    for (int page = 1; page <= 3; page++)
                    {
                        using StreamWriter writer = new(archive.CreateEntry($"ppt/slides/slide{page}.xml").Open());
                        string text = CapitalizationSample ? "EVERYONE SHOULD READ THIS SAMPLE TEXT with God and the USA." : $"Source {page}";
                        writer.Write($"<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"{(page == 2 ? " show=\"0\"" : string.Empty)}><a:p><a:r><a:t>{text}</a:t></a:r></a:p></p:sld>");
                    }
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(stream.ToArray()) };
            }
            if (path.EndsWith(".jpg", StringComparison.Ordinal))
            {
                if (request.RequestUri.Query != "?signature=unchanged") throw new InvalidOperationException("A signed export URL was modified.");
                if (FailImageDownload) return new(HttpStatusCode.Forbidden);
                byte[] image = InvalidImage ? Encoding.UTF8.GetBytes("not an image")
                    : WindowsReleaseVerification.CreateVerificationImage((byte)(int.Parse(Path.GetFileNameWithoutExtension(path)) * 40));
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(image) };
            }
            if (path.EndsWith("/pages", StringComparison.Ordinal))
                return Json("{\"items\":[" + string.Join(",", Enumerable.Range(1, 3).Select(page =>
                    $"{{\"page_number\":{page},\"id\":\"source-{page}\",\"dimensions\":{{\"width\":{PageWidth},\"height\":1080}},\"thumbnail\":{{\"url\":\"https://test.invalid/stale.png\"}}}}")) + "]}");
            return Json("{\"design\":{\"id\":\"hidden-deck\",\"title\":\"Hidden deck\",\"design_types\":[\"presentation\"],\"page_count\":3,\"updated_at\":1893456000}}");
        }

        private static HttpResponseMessage Json(string content)
            => new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    }
}
