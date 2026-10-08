using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using CanvaStage.Windows.Controls;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Data;
using Size = System.Windows.Size;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using ComboBox = System.Windows.Controls.ComboBox;

namespace CanvaStage.Windows.Services;

/// <summary>Checks simplified navigation and copies of the real output, not separately reflowed text.</summary>
internal static class OperatorWorkflowVerification
{
    public static async Task RunAsync(MainWindow main, MainViewModel model, OutputWindow output,
        IOutputCoordinator coordinator, string directory, Action<FrameworkElement, string, double> capture)
    {
        FrameworkElement setup = (FrameworkElement)main.FindName("SetupPanel");
        FrameworkElement settings = (FrameworkElement)main.FindName("SettingsPanel");
        FrameworkElement compact = (FrameworkElement)main.FindName("CompactViewRoot");
        FrameworkElement live = (FrameworkElement)main.FindName("LiveControlGrid");
        Require(!Descendants(output).OfType<MediaElement>().Any(), "Confidence output unexpectedly contains an audio/video player");
        // Settings retains its own sync control, but it is not shown on Live control.
        Require(Descendants(main).OfType<ButtonBase>().Count(button => button.IsVisible
            && ReferenceEquals(button.Command, model.TogglePresenterSyncCommand)) == 1,
            "Sync controls are duplicated instead of remaining beside the main status");
        Require(!Descendants(live).OfType<ButtonBase>().Any(button => ReferenceEquals(button.Command, model.TestSetupCommand)),
            "Sunday controls still duplicate Sync details");
        FrameworkElement modes = (FrameworkElement)main.FindName("SundayModeControls");
        System.Windows.Controls.Primitives.ToggleButton[] modeButtons = Descendants(live)
            .OfType<System.Windows.Controls.Primitives.ToggleButton>().ToArray();
        Require(modeButtons.Length == 2 && Descendants(modes).OfType<System.Windows.Controls.Primitives.ToggleButton>().Count() == 2,
            "Mirror/Monitor controls are duplicated or missing from Sunday controls");
        foreach (System.Windows.Controls.Primitives.ToggleButton button in modeButtons)
        {
            string? bindingPath = BindingOperations.GetBinding(button, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.Path.Path;
            button.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
            button.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.UpdateSource();
            Require(model.OutputMode == (bindingPath == nameof(MainViewModel.IsMirrorSelected) ? OutputMode.Mirror : OutputMode.Monitor),
                "Relocated mode controls do not update the output");
        }
        Require(main.Title == model.ApplicationVersionLabel && model.ApplicationVersionLabel == ApplicationVersion.Label
            && ApplicationVersion.Current.Split('-')[0] == typeof(MainViewModel).Assembly.GetName().Version?.ToString(3),
            "Visible app version does not match the build version");
        RoundedPreviewBorder frame = (RoundedPreviewBorder)main.FindName("LivePreviewFrame");
        Require(frame.Margin.Top == 12 && frame.Margin.Bottom <= 8, "Preview label spacing does not match the latest request");
        Require(!Descendants(settings).OfType<Expander>().Any(), "Settings sections still require expansion");
        System.Windows.Controls.TextBox settingsInput = Descendants(settings).OfType<System.Windows.Controls.TextBox>().First();
        ComboBox setupInput = Descendants(setup).OfType<ComboBox>().First();
        Require(settingsInput.FontSize == setupInput.FontSize, "Settings content inherits oversized text");
        model.ActiveSection = OperatorSection.SettingsDiagnostics;
        main.ScrollContentToTopForAutomation();
        await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        capture(main, Path.Combine(directory, "settings-always-visible-connection.png"), 1);
        ((FrameworkElement)main.FindName("ContentCacheSettings")).BringIntoView();
        await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        capture(main, Path.Combine(directory, "settings-always-visible-cache.png"), 1);
        model.ActiveSection = OperatorSection.LiveControl;
        main.ScrollContentToTopForAutomation();
        await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await VerifyPreviewContainersAsync(main, model, output, directory, capture);
        VerifyRoundedPreview(model, output, directory, capture);
        Require(!Descendants(setup).OfType<PasswordBox>().Any(), "Setup exposes a secret entry");
        Require(Descendants(settings).OfType<PasswordBox>().Count() == 1, "Settings does not own the secret entry");
        Require(!Descendants(compact).OfType<ButtonBase>().Any(button => ReferenceEquals(button.Command, model.TogglePresenterSyncCommand)),
            "Compact view exposes a pause/resume button");
        Require(!Descendants(live).OfType<ComboBox>().Any(), "Live control still exposes advanced settings");
        Require(Descendants(setup).OfType<ComboBox>().Any(box => ReferenceEquals(box.ItemsSource, model.Displays)),
            "Setup has no confidence display selector");
        Require(Descendants(setup).OfType<ComboBox>().Any(box => ReferenceEquals(box.ItemsSource, model.CanvaPresentations)),
            "Setup has no presentation selector");

        // Opening or closing Preview must not move or stop the live display.
        System.Drawing.Rectangle nativeBounds = output.GetNativeBounds();
        model.PreviewCommand.Execute(null);
        Window previewWindow = model.OutputManagerForVerification.PreviewWindowForVerification!;
        await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Require(output.IsVisible && coordinator.Snapshot.IsLive && output.GetNativeBounds() == nativeBounds,
            "Opening Preview displaced or stopped live output");
        Require(previewWindow.Content is ConfidencePreview preview && ReferenceEquals(preview.SourceVisual, output.PreviewSurface),
            "Separate Preview uses a different rendering surface");
        capture(previewWindow, Path.Combine(directory, "separate-preview-live-output.png"), 1);
        previewWindow.Close();
        Require(output.IsVisible && coordinator.Snapshot.IsLive, "Closing Preview stopped live output");

        // 1:1 pixel comparison in an isolated fixture. At smaller preview sizes the
        // very same visual is uniformly scaled; no TextBlock is remeasured.
        ConfidencePreview fixture = new() { DataContext = model };
        foreach (OutputMode mode in Enum.GetValues<OutputMode>())
        {
            coordinator.SetOutputMode(mode);
            foreach (MonitorLayout layout in Enum.GetValues<MonitorLayout>())
            {
                coordinator.SetLayout(layout);
                coordinator.SelectPage(1);
                await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                VerifyPixels(fixture, output.PreviewSurface);
                Size original = output.PreviewSurface.RenderSize;
                fixture.Measure(new Size(320, 180));
                fixture.Arrange(new Rect(0, 0, 320, 180));
                fixture.UpdateLayout();
                Require(output.PreviewSurface.RenderSize == original, "Resizing Preview reflowed the output");
                capture(fixture, Path.Combine(directory, $"preview-scaled-{mode}-{layout}.png"), 1);
            }
        }
        coordinator.SetOutputMode(OutputMode.Monitor);
        coordinator.SetBlanked(true);
        await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        VerifyPixels(fixture, output.PreviewSurface);
        coordinator.SetBlanked(false);
        model.OutputManagerForVerification.StopOutput();
        model.PreviewCommand.Execute(null);
        await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Require(!coordinator.Snapshot.IsLive && !output.IsVisible && output.PreviewSurface.ActualWidth > 0,
            "Offline Preview started a live session or has no viewport");
        Require(((OutputWindowViewModel)output.DataContext).IsMonitor, "Offline Preview does not show the prepared deck");
        OutputWindowViewModel outputModel = (OutputWindowViewModel)output.DataContext;
        Require(outputModel.Page == coordinator.Snapshot.Document.FindPage(coordinator.Snapshot.SelectedPageNumber)
            && !string.IsNullOrWhiteSpace(outputModel.PrimaryText), "Offline Preview lost its cached page text");
        using (OutputWindowViewModel reopened = new(coordinator))
            Require(reopened.Page == outputModel.Page && reopened.PrimaryText == outputModel.PrimaryText,
                "A newly constructed offline renderer lost the cached selected page");
        coordinator.SelectPage(3);
        await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Require(outputModel.PrimaryText == coordinator.Snapshot.Document.FindPage(3)?.PrimaryText
            && ((System.Windows.Controls.TextBlock)output.FindName("PrimaryText")).Text == outputModel.PrimaryText,
            "Offline page navigation did not update the shared output visual");
        VerifyPixels(fixture, output.PreviewSurface);
        capture(model.OutputManagerForVerification.PreviewWindowForVerification!, Path.Combine(directory, "separate-preview-offline.png"), 1);
        await VerifyOfflineImagesAsync(model, output, coordinator, directory, capture);
        model.OutputManagerForVerification.PreviewWindowForVerification!.Close();
    }

    private static async Task VerifyPreviewContainersAsync(MainWindow main, MainViewModel model,
        OutputWindow output, string directory, Action<FrameworkElement, string, double> capture)
    {
        double originalWidth = main.Width, originalHeight = main.Height;
        double outputWidth = output.Width, outputHeight = output.Height;
        bool originalOnTop = model.IsOperatorTopmost;
        try
        {
            model.ActiveSection = OperatorSection.LiveControl;
            // A saved enabled choice must never make Full view topmost.
            model.IsOperatorTopmost = true;
            await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Require(!main.Topmost && !model.IsWindowTopmost, "Full view applies the compact on-top preference");
            Require(!Descendants(main.FindName("FullViewRoot") as DependencyObject ?? main)
                .OfType<System.Windows.Controls.Primitives.ToggleButton>()
                .Any(button => BindingOperations.GetBinding(button, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.Path.Path == nameof(MainViewModel.IsOperatorTopmost)),
                "Full view still exposes an on-top control");

            foreach ((int width, int height) in new[] { (1280, 720), (1024, 768), (720, 1280) })
            {
                VerificationWindowSizing.SetSize(output, width, height);
                foreach (double operatorWidth in new[] { 1380d, 900d, 640d })
                {
                    VerificationWindowSizing.SetSize(main, operatorWidth, 900);
                    await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    VerifyWholeFrame((RoundedPreviewBorder)main.FindName("LivePreviewFrame"), output);
                }
                capture(main, Path.Combine(directory, $"live-preview-aspect-{width}x{height}.png"), 1);
                main.SetCompactViewForAutomation(true);
                await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Require(main.Topmost && model.IsWindowTopmost, "Compact view does not restore its saved on-top choice");
                RoundedPreviewBorder compactFrame = (RoundedPreviewBorder)main.FindName("CompactPreviewFrame");
                VerifyWholeFrame(compactFrame, output);
                Require(main.SizeToContent == SizeToContent.Height && main.ActualHeight <= main.MaxHeight + 1,
                    "Compact view does not grow to fit within the screen work area");
                capture(main, Path.Combine(directory, $"compact-preview-aspect-{width}x{height}.png"), 1);
                main.Width = 360;
                await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                VerifyWholeFrame(compactFrame, output);
                System.Windows.Controls.Primitives.ToggleButton onTop = (System.Windows.Controls.Primitives.ToggleButton)main.FindName("CompactOnTopButton");
                onTop.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
                onTop.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.UpdateSource();
                Require(!main.Topmost && !model.IsOperatorTopmost, "Compact on-top toggle does not disable topmost");
                model.IsOperatorTopmost = true;
                main.SetCompactViewForAutomation(false);
                await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Require(!main.Topmost && model.IsOperatorTopmost, "Leaving Compact view does not clear effective topmost while retaining its preference");
            }
        }
        finally
        {
            main.SetCompactViewForAutomation(false);
            model.IsOperatorTopmost = originalOnTop;
            VerificationWindowSizing.SetSize(output, outputWidth, outputHeight);
            VerificationWindowSizing.SetSize(main, originalWidth, originalHeight);
            await main.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }
    }

    private static void VerifyWholeFrame(RoundedPreviewBorder frame, OutputWindow output)
    {
        Require(frame.Child is AspectRatioPreviewHost, "Preview lacks an aspect-ratio container");
        AspectRatioPreviewHost host = (AspectRatioPreviewHost)frame.Child;
        Require(host.Child is ConfidencePreview, "Preview does not copy the shared output visual");
        ConfidencePreview preview = (ConfidencePreview)host.Child;
        double ratio = output.PreviewSurface.ActualWidth / output.PreviewSurface.ActualHeight;
        Require(preview.ActualWidth > 0 && preview.ActualHeight > 0
            && Math.Abs(preview.ActualHeight - preview.ActualWidth / ratio) <= 1
            && Math.Abs(host.ActualWidth - preview.ActualWidth) <= 1
            && Math.Abs(host.ActualHeight - preview.ActualHeight) <= 1
            && ReferenceEquals(preview.SourceVisual, output.PreviewSurface)
            && host.Clip is RectangleGeometry,
            "Preview is cropped, letterboxed inside its frame, unrounded or reflows a different source");
    }

    private static void VerifyRoundedPreview(MainViewModel model, OutputWindow output, string directory,
        Action<FrameworkElement, string, double> capture)
    {
        ConfidencePreview preview = new() { DataContext = model };
        RoundedPreviewBorder frame = new() { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1),
            Background = System.Windows.Media.Brushes.Black, Child = preview };
        foreach (Size size in new[] { new Size(320, 180), new Size(640, 360) })
        {
            frame.Measure(size);
            frame.Arrange(new Rect(size));
            frame.UpdateLayout();
            Require(preview.Clip is RectangleGeometry clip && clip.RadiusX > 0 && clip.Rect.Size == preview.RenderSize
                && !clip.FillContains(new System.Windows.Point(0, 0))
                && clip.FillContains(new System.Windows.Point(preview.ActualWidth / 2, preview.ActualHeight / 2)),
                "Preview corners are not clipped after resizing");
            Require(ReferenceEquals(preview.SourceVisual, output.PreviewSurface) && output.PreviewSurface.Clip is null,
                "Rounded operator preview changed the source output visual");
            RenderTargetBitmap bitmap = new((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(frame);
            byte[] corner = new byte[4];
            bitmap.CopyPixels(new Int32Rect(0, 0, 1, 1), corner, 4, 0);
            Require(corner[3] == 0, "Square preview content covers the rounded frame corner");
        }
        capture(frame, Path.Combine(directory, "rounded-operator-preview.png"), 1);
    }

    private static async Task VerifyOfflineImagesAsync(MainViewModel model, OutputWindow output,
        IOutputCoordinator coordinator, string directory, Action<FrameworkElement, string, double> capture)
    {
        PresentationDocument original = coordinator.Snapshot.Document;
        int originalPage = coordinator.Snapshot.SelectedPageNumber;
        string imagePath = Path.Combine(directory, "isolated-storage", "offline-preview.png");
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null,
            new byte[] { 255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255 }, 8)));
        using (FileStream stream = File.Create(imagePath)) encoder.Save(stream);
        try
        {
            coordinator.SetDocument(new("offline-preview", "Offline image fixture",
                [new(1, "Image only", [], "#080B11", "#8454F4", HasFormattedText: false, FallbackImagePath: imagePath)],
                DateTimeOffset.UtcNow));
            OutputWindowViewModel outputModel = (OutputWindowViewModel)output.DataContext;
            foreach (OutputMode mode in Enum.GetValues<OutputMode>())
            {
                coordinator.SetOutputMode(mode);
                await output.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Require(!coordinator.Snapshot.IsLive && coordinator.Snapshot.CurrentPage is null && !output.IsVisible
                    && outputModel.HasFallbackImage && (mode != OutputMode.Monitor || outputModel.ShowFallbackImage),
                    "Offline Mirror/Monitor fallback did not retain its cached image without starting output");
                capture(model.OutputManagerForVerification.PreviewWindowForVerification!,
                    Path.Combine(directory, $"separate-preview-offline-image-{mode}.png"), 1);
            }
            using OutputWindowViewModel reopened = new(coordinator);
            Require(reopened.HasFallbackImage && reopened.ShowFallbackImage,
                "A newly constructed offline renderer did not load its cached fallback image");
        }
        finally
        {
            coordinator.SetDocument(original, originalPage);
            coordinator.SetOutputMode(OutputMode.Monitor);
        }
    }

    private static void VerifyPixels(ConfidencePreview fixture, FrameworkElement source)
    {
        Size size = source.RenderSize;
        Require(size.Width > 0 && size.Height > 0, "Output surface has no viewport");
        fixture.Measure(size);
        fixture.Arrange(new Rect(size));
        fixture.UpdateLayout();
        Require(ReferenceEquals(fixture.SourceVisual, source), "Preview source is not the actual output");
        int width = (int)Math.Ceiling(size.Width), height = (int)Math.Ceiling(size.Height);
        byte[] ReadPixels(FrameworkElement visual)
        {
            RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            byte[] bytes = new byte[checked(width * height * 4)];
            bitmap.CopyPixels(bytes, width * 4, 0);
            return bytes;
        }
        byte[] expected = ReadPixels(source), actual = ReadPixels(fixture);
        long difference = 0;
        for (int index = 0; index < expected.Length; index++) difference += Math.Abs(expected[index] - actual[index]);
        // Small antialiasing differences are possible through a VisualBrush.
        Require((double)difference / expected.Length < 2, "Preview pixel content differs from output");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (object item in LogicalTreeHelper.GetChildren(parent))
        {
            if (item is not DependencyObject child) continue;
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }
}
