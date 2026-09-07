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
        PresenterSource source = new("window-1", "Sunday verification - Canva", "Chrome", document.Id, 2, 3);
        void Feed(PresenterSource[] sources) => viewModel.ReceivePresenterUpdateForVerification(
            new(CanvaPresenterTrackingState.PageDetected, null, null, "Synthetic Presenter", DateTimeOffset.UtcNow, sources));

        viewModel.IsPresenterSyncEnabled = true;
        Feed([source]);
        Require(viewModel.CurrentPageNumber == 2, "Verified Presenter selects its page");
        viewModel.PreviousPageCommand.Execute(null);
        Feed([source with { CurrentPage = 3 }]);
        Require(viewModel.CurrentPageNumber == 1 && viewModel.PresenterSyncSummary.StartsWith("Paused"), "Manual navigation pauses following");
        viewModel.TogglePresenterSyncCommand.Execute(null);
        Require(viewModel.CurrentPageNumber == 3, "Resume uses current fresh Presenter page");
        Feed([source, source with { Key = "window-2", CurrentPage = 1 }]);
        Require(!viewModel.IsPresenterWindowChoiceNeeded && viewModel.CurrentPageNumber == 3, "Previously followed automatic window remains stable when a duplicate appears");
        viewModel.AutomaticPresenterWindowCommand.Execute(null);
        Feed([source, source with { Key = "window-2", CurrentPage = 1 }]);
        Require(viewModel.IsPresenterWindowChoiceNeeded && viewModel.CurrentPageNumber == 3, "Ambiguous initial windows hold the last page");
        viewModel.SelectedPresenterSource = viewModel.PresenterSources.Last();
        viewModel.ConfirmPresenterWindowCommand.Execute(null);
        Require(viewModel.CurrentPageNumber == 1, "Explicit window confirmation selects intended source");
        Feed([source]);
        Require(viewModel.CurrentPageNumber == 1 && viewModel.PresenterSyncSummary == "Waiting for Presenter", "Missing confirmed window does not switch to another");
        viewModel.AutomaticPresenterWindowCommand.Execute(null);
        Feed([source with { DesignId = "other-design" }]);
        Require(viewModel.PresenterSyncSummary == "Presentation mismatch", "Wrong design cannot follow despite matching title and count");
        Feed([source]);
        viewModel.IsPresenterSyncEnabled = false;
        Feed([source with { CurrentPage = 3 }]);
        Require(viewModel.CurrentPageNumber == 2, "Disabled sync ignores queued updates");
        viewModel.IsPresenterSyncEnabled = true;
        viewModel.ReceivePresenterUpdateForVerification(new(CanvaPresenterTrackingState.Error, null, null, "Retrying automatically…", DateTimeOffset.UtcNow));
        Require(viewModel.PresenterSyncSummary == "Reconnecting to Presenter", "Scan failure has actionable recovery status");
        Feed([source]);
        Require(viewModel.PresenterSyncSummary == "Following Canva", "Recovered scan resumes following");
        checks.Add("Presenter identity, sticky automatic selection, ambiguity, explicit selection, manual pause, resume, disabled updates and recovery");

        viewModel.NextLineFontSize = 48;
        viewModel.IsNextLineBold = true;
        viewModel.NextLineColor = "#75E6C8";
        Require(coordinator.Snapshot.Appearance is { NextLineFontSize: 48, IsNextLineBold: true, NextLineColor: "#75E6C8" },
            "Next-line size, weight and color apply immediately");
        checks.Add("Custom next-line size, weight and color apply to Preview and output");
        Require(mainWindow.Icon is not null, "Operator window loads the CanvaStage icon");

        await VerifyCredentialsAndCacheAsync(directory);
        checks.Add("DPAPI reopen and blank-secret reuse; refresh while previous image is locked; cache restoration");
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

        viewModel.ActiveSection = OperatorSection.LiveControl;
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
        VerificationWindowSizing.Configure(output);
        try
        {
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
                    output.VerifyTextFit(scale);
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
            new(2, "Welcome", ["Welcome to our Sunday service"], "#0D0E10", "#75E6C8"),
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
        using HttpClient client = new(new SyntheticCanvaHandler());
        CanvaConnectService service = new(reopened, client, Path.Combine(storage, "cache"));
        service.SaveConfiguration(credentials.ClientId, "");
        Require(reopened.Load() == credentials, "Blank input reuses saved secret without clearing tokens");
        PresentationDocument first = await service.LoadAsync("verification-design");
        string originalPath = first.Pages[0].FallbackImagePath!;
        using FileStream lockedImage = new(originalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        PresentationDocument second = await service.LoadAsync("verification-design");
        Require(second.Pages[0].FallbackImagePath != originalPath && File.Exists(originalPath), "Refresh uses new image paths while old images remain open");
        PresentationCacheStore cache = new(Path.Combine(storage, "cache"));
        cache.Save(second);
        Require(new PresentationCacheStore(Path.Combine(storage, "cache")).Load()?.Pages[0].FallbackImagePath == second.Pages[0].FallbackImagePath,
            "Refreshed cache restores across restart");
    }

    private sealed class SyntheticCanvaHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
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
            else if (path.EndsWith(".png", StringComparison.Ordinal))
                content = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a7WQAAAAASUVORK5CYII="));
            else
            {
                string json = path.EndsWith("/pages", StringComparison.Ordinal)
                    ? "{\"items\":[{\"page_number\":1,\"id\":\"page1\",\"thumbnail\":{\"url\":\"https://test.invalid/page.png\"}}]}"
                    : path.EndsWith("/exports", StringComparison.Ordinal)
                        ? "{\"job\":{\"id\":\"export1\",\"status\":\"success\",\"urls\":[\"https://test.invalid/deck.pptx\"]}}"
                        : "{\"design\":{\"id\":\"verification-design\",\"title\":\"Sunday verification\",\"design_types\":[\"presentation\"],\"page_count\":1}}";
                content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
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
