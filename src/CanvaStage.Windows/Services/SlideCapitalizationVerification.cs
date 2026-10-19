using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Core.Utilities;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;
using CheckBox = System.Windows.Controls.CheckBox;

namespace CanvaStage.Windows.Services;

internal static class SlideCapitalizationVerification
{
    public static async Task RunAsync(string directory, Action<Window, string, double> capture)
    {
        const string native = "EVERYONE SHOULD READ THIS SAMPLE TEXT with God and the USA.";
        const string visible = "Everyone should read this sample text with God and the USA.";
        string storage = Path.Combine(directory, "isolated-storage", "capitalization");
        PresentationPage page = new(1, native, [native], "#000000", "#FFFFFF", SlideCapitalizationTextBlocks: [visible], UseSlideCapitalization: true);
        PresentationDocument document = new("sample-case", "Sample deck", [page], DateTimeOffset.UtcNow,
            WasSlideCapitalizationChecked: true, CapitalizationAdjustedPages: 1);
        PresentationCacheStore cache = new(Path.Combine(storage, "cache"));
        cache.Save(document);
        PresentationDocument reopened = cache.Load()!;
        Require(reopened.Pages[0].PrimaryText == visible && reopened.WithSlideCapitalization(false).Pages[0].PrimaryText == native,
            "Cache restart loses original or matched text");
        AppPreferences preferences = AppPreferences.Default with { IsAutomaticContentRefreshEnabled = false };
        OutputCoordinator coordinator = new(reopened, preferences);
        OutputWindowManager manager = new(coordinator);
        DisplayService displays = new();
        using HttpClient client = new();
        CanvaConnectService canva = new(new CanvaCredentialsStore(storage), client, Path.Combine(storage, "cache"));
        MainViewModel model = new(coordinator, manager, displays, new DisplayIdentificationService(displays), new DiagnosticsExportService(),
            new JsonPreferencesStore(storage), cache, canva, new WindowsReleaseVerification.DemoTracker(), preferences,
            displayMirror: new DisplayMirrorService(storage));
        MainWindow window = new(model);
        using OutputWindowViewModel output = new(coordinator);
        try
        {
            window.Show();
            VerificationWindowSizing.Configure(window);
            VerificationWindowSizing.SetSize(window, 1380, 900);
            model.ActiveSection = OperatorSection.SettingsDiagnostics;
            coordinator.StartOutput();
            coordinator.SetBlanked(true);
            model.MatchSlideCapitalization = true;
            Require(output.PrimaryText == visible && coordinator.Snapshot.IsLive && coordinator.Snapshot.IsBlanked,
                "Enabling changes live/blank state or fails to update output text");
            Require(new JsonPreferencesStore(storage).Load().MatchSlideCapitalization, "Setting is not persisted");
            CheckBox toggle = (CheckBox)window.FindName("MatchSlideCapitalizationToggle");
            Require(toggle.IsChecked == true, "Checkbox is unbound");
            ((FrameworkElement)window.FindName("SlideCapitalizationSettings")).BringIntoView();
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            capture(window, Path.Combine(directory, "settings-slide-capitalization.png"), 1);
            VerificationWindowSizing.SetSize(window, 1000, 740);
            ((FrameworkElement)window.FindName("SlideCapitalizationSettings")).BringIntoView();
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
            capture(window, Path.Combine(directory, "settings-slide-capitalization-narrow.png"), 1.5);
            model.MatchSlideCapitalization = false;
            Require(output.PrimaryText == native && coordinator.Snapshot.IsBlanked && coordinator.Snapshot.IsLive,
                "Disabling fails to restore native output or changes session state");
            Require(!new JsonPreferencesStore(storage).Load().MatchSlideCapitalization, "Off choice does not survive restart");
        }
        finally { coordinator.StopOutput(); window.Close(); }

        string image = Path.Combine(storage, "ocr-sample.png");
        DrawingVisual drawing = new();
        using (DrawingContext context = drawing.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, 1200, 220));
            context.DrawText(new FormattedText("Everyone should read this sample text.\nKeep this line unchanged.", CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight, new Typeface("Arial"), 36, Brushes.Black, 1), new System.Windows.Point(30, 30));
        }
        RenderTargetBitmap bitmap = new(1200, 220, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream file = File.Create(image)) encoder.Save(file);
        string? real = await WindowsSlideTextRecognizer.ReadAsync(image, CancellationToken.None);
        // No language installed is a supported failure; the import checks exercise it.
        if (real is not null)
            Require(SlideCapitalizationMatcher.Match("EVERYONE SHOULD READ THIS SAMPLE TEXT. Keep this line unchanged.", [real])
                .StartsWith("Everyone should read this sample text."), "Native OCR failed on the neutral fixture");
        PrivacySafeLog.Write($"Capitalization verification: native OCR available {real is not null}; neutral fixture only.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Capitalization verification: " + message);
    }
}
