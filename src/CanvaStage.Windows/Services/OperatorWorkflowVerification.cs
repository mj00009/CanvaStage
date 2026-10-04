using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
        capture(model.OutputManagerForVerification.PreviewWindowForVerification!, Path.Combine(directory, "separate-preview-offline.png"), 1);
        model.OutputManagerForVerification.PreviewWindowForVerification!.Close();
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
