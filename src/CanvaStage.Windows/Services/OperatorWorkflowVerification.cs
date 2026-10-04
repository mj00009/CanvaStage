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
        Require(Descendants(main).OfType<ButtonBase>().Count(button => ReferenceEquals(button.Command, model.TogglePresenterSyncCommand)) == 1,
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
        Require(frame.Margin.Top <= 6 && frame.Margin.Bottom <= 8, "Preview vertical spacing regressed");
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
