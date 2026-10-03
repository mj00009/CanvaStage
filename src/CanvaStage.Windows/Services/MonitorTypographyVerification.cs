using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Windows.Controls;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TextBlock = System.Windows.Controls.TextBlock;

namespace CanvaStage.Windows.Services;

/// <summary>Observes the real layout before painting, not a manually repaired screenshot.</summary>
internal static class MonitorTypographyVerification
{
    public static async Task RunAsync(MainWindow mainWindow, MainViewModel mainViewModel,
        OutputWindow output, IOutputCoordinator coordinator, string directory,
        Action<FrameworkElement, string, double> capture)
    {
        mainViewModel.ActiveSection = OperatorSection.LiveControl;
        mainWindow.ScrollContentToTopForAutomation();
        VerificationWindowSizing.SetSize(mainWindow, 1380, 900);
        VerificationWindowSizing.SetSize(output, 1000, 560);
        coordinator.SetOutputMode(OutputMode.Monitor);
        coordinator.SetLayout(MonitorLayout.CurrentAndNext);
        coordinator.SetAppearance(MonitorAppearance.Default with { FontSize = 160, NextLineFontSize = 24 });
        await mainWindow.Dispatcher.InvokeAsync(() => mainWindow.UpdateLayout(), DispatcherPriority.ApplicationIdle);
        ConfidencePreview preview = Descendants(mainWindow).OfType<ConfidencePreview>().First(element => element.IsVisible);
        FittedTextHost outputHost = (FittedTextHost)output.FindName("PrimaryTextHost");
        FittedTextHost previewHost = (FittedTextHost)preview.FindName("PreviewTextHost");
        TextBlock primary = (TextBlock)outputHost.Child;
        TextBlock next = (TextBlock)output.FindName("NextLineText");
        TextBlock previewPrimary = (TextBlock)previewHost.Child;
        TextBlock previewNext = (TextBlock)preview.FindName("PreviewNextLineText");

        async Task ObserveChangeAsync(Action change, bool waitForWindowSize = false)
        {
            TaskCompletionSource<(double OutputSize, double PreviewSize)> firstFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Exception? observedFailure = null;
            void ObserveFrame(object? sender, EventArgs args)
            {
                try
                {
                    // Native resize messages can arrive after an old-size frame. That
                    // frame is valid for its old viewport; inspect the first new-size frame.
                    if (waitForWindowSize && (Math.Abs(output.ActualWidth - output.Width) > 1
                        || Math.Abs(output.ActualHeight - output.Height) > 1)) return;
                    if (primary.Text != ((OutputWindowViewModel)output.DataContext).PrimaryText
                        || previewPrimary.Text != mainViewModel.PreviewPrimaryText)
                        throw new InvalidOperationException("The first rendered frame has stale confidence text.");
                    if (!outputHost.TextFits || !previewHost.TextFits)
                        throw new InvalidOperationException("The first rendered frame contains oversized confidence text.");
                    firstFrame.TrySetResult((primary.FontSize, previewPrimary.FontSize));
                }
                catch (Exception exception) { observedFailure = exception; firstFrame.TrySetException(exception); }
            }
            CompositionTarget.Rendering += ObserveFrame;
            try
            {
                change();
                output.InvalidateVisual();
                (double outputSize, double previewSize) = await firstFrame.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await output.Dispatcher.InvokeAsync(() => { output.UpdateLayout(); mainWindow.UpdateLayout(); }, DispatcherPriority.ApplicationIdle);
                if (observedFailure is not null) throw observedFailure;
                // Windows can present multiple intermediate client viewports while resizing.
                // Every frame must fit; only fixed-viewport changes require an identical
                // first/settled font size (the regression reported during slide changes).
                if (!waitForWindowSize && (Math.Abs(primary.FontSize - outputSize) > 0.01 || Math.Abs(previewPrimary.FontSize - previewSize) > 0.01))
                    throw new InvalidOperationException($"Confidence text changed size after its first rendered frame: output {outputSize:0.00} -> {primary.FontSize:0.00}; preview {previewSize:0.00} -> {previewPrimary.FontSize:0.00}.");
                output.VerifyTextFit();
            }
            finally { CompositionTarget.Rendering -= ObserveFrame; }
        }

        // Short -> long -> short, including changes which alter the available text area.
        foreach (int page in new[] { 3, 1, 3, 2, 1 })
            await ObserveChangeAsync(() => coordinator.SelectPage(page));
        foreach (string font in new[] { "Aptos", "Arial", "Segoe UI", "Verdana" })
        {
            await ObserveChangeAsync(() => coordinator.SetAppearance(coordinator.Snapshot.Appearance with { FontFamily = font }));
            if (primary.FontFamily.Source != font || next.FontFamily.Source != font
                || previewPrimary.FontFamily.Source != font || previewNext.FontFamily.Source != font
                || primary.FontWeight != next.FontWeight || previewPrimary.FontWeight != previewNext.FontWeight
                || next.FontSize != 24 || previewNext.FontSize != mainViewModel.PreviewNextLineFontSize
                || primary.FontSize >= 160)
                throw new InvalidOperationException("Current/Next typography does not share the selected family/default weight with independent sizes.");
            capture(output, Path.Combine(directory, $"output-font-match-{font.Replace(' ', '-')}.png"), 1);
        }
        await ObserveChangeAsync(() => coordinator.SetAppearance(coordinator.Snapshot.Appearance with { IsNextLineBold = true }));
        if (next.FontWeight != FontWeights.Bold || previewNext.FontWeight != FontWeights.Bold)
            throw new InvalidOperationException("The optional Next bold override was lost.");
        await ObserveChangeAsync(() => coordinator.SetAppearance(coordinator.Snapshot.Appearance with { IsNextLineBold = false, ShowClock = false, FontSize = 96 }));
        foreach (MonitorLayout layout in Enum.GetValues<MonitorLayout>())
            await ObserveChangeAsync(() => coordinator.SetLayout(layout));
        foreach ((double width, double height) in new[] { (800d, 450d), (1280d, 720d), (1000d, 560d) })
            await ObserveChangeAsync(() => VerificationWindowSizing.SetSize(output, width, height), waitForWindowSize: true);
        coordinator.SetLayout(MonitorLayout.CurrentAndNext);
        capture(mainWindow, Path.Combine(directory, "operator-LiveControl-matching-fonts.png"), 1);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }
}
