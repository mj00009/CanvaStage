using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace CanvaStage.Windows.Services;

internal static class OutputEffectsVerification
{
    public static async Task RunAsync(MainWindow main, MainViewModel viewModel, OutputWindow output,
        IOutputCoordinator coordinator, string directory, Action<FrameworkElement, string, double> capture)
    {
        OutputSnapshot saved = coordinator.Snapshot;
        TextBlock mirrorClock = (TextBlock)output.FindName("MirrorClock");
        TextBlock monitorClock = (TextBlock)output.FindName("MonitorClock");
        async Task SettleAsync()
        {
            TaskCompletionSource frame = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnFrame(object? sender, EventArgs args) => frame.TrySetResult();
            CompositionTarget.Rendering += OnFrame;
            try
            {
                await output.Dispatcher.InvokeAsync(() => output.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                await frame.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally { CompositionTarget.Rendering -= OnFrame; }
        }
        try
        {
            coordinator.SetOutputMode(OutputMode.Mirror);
            coordinator.SetAppearance(MonitorAppearance.Default);
            coordinator.StartOutput();
            await SettleAsync();
            Require(!mirrorClock.IsVisible, "Mirror clock is off for existing preferences");
            FrameworkElement fallback = (FrameworkElement)output.FindName("MirrorFallbackContent");
            FrameworkElement fallbackBounds = (FrameworkElement)output.FindName("MirrorFallbackBounds");
            Rect fittedBounds = fallback.TransformToAncestor(fallbackBounds).TransformBounds(new Rect(fallback.RenderSize));
            Require(fittedBounds.Top >= -1 && fittedBounds.Left >= -1
                && fittedBounds.Bottom <= fallbackBounds.ActualHeight + 1 && fittedBounds.Right <= fallbackBounds.ActualWidth + 1,
                "Long Mirror placeholder text scales inside its viewport when no cached image is available");
            capture(output, Path.Combine(directory, "output-mirror-long-placeholder.png"), 1);
            string imageDirectory = Path.Combine(directory, "isolated-storage", "clock-images");
            Directory.CreateDirectory(imageDirectory);
            PresentationPage[] imagePages = saved.Document.Pages.Select(page =>
            {
                string imagePath = Path.Combine(imageDirectory, $"page-{page.Number}.png");
                byte tone = page.Number == 2 ? (byte)240 : (byte)40;
                byte[] pixels = Enumerable.Range(0, 64).SelectMany(_ => new byte[] { tone, tone, tone, 255 }).ToArray();
                BitmapSource image = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Bgra32, null, pixels, 32);
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (FileStream file = File.Create(imagePath)) encoder.Save(file);
                return page with { FallbackImagePath = imagePath };
            }).ToArray();
            PresentationDocument imageDocument = saved.Document with { Pages = imagePages };
            coordinator.SetDocument(imageDocument);
            viewModel.ShowMirrorClock = true;
            viewModel.MirrorClockFontSize = 64;
            viewModel.ClockFontSize = 48;
            viewModel.IsClockBold = false;
            viewModel.IsMirrorClockOnLeft = true;
            await SettleAsync();
            Require(mirrorClock.IsVisible && mirrorClock.FontSize == 64 && mirrorClock.FontWeight == FontWeights.Normal
                && mirrorClock.HorizontalAlignment == HorizontalAlignment.Left && mirrorClock.Effect is DropShadowEffect,
                "Mirror clock applies size, regular weight, left alignment and shadow");
            Require(((OutputWindowViewModel)output.DataContext).HasFallbackImage,
                "Mirror clock overlays a decoded cached slide image");
            capture(output, Path.Combine(directory, "output-mirror-clock-left.png"), 1);
            viewModel.IsMirrorClockOnLeft = false;
            viewModel.IsClockBold = true;
            await SettleAsync();
            Require(mirrorClock.HorizontalAlignment == HorizontalAlignment.Right && mirrorClock.FontWeight == FontWeights.SemiBold,
                "Mirror clock supports right alignment and a slightly bold weight");
            capture(output, Path.Combine(directory, "output-mirror-clock-right.png"), 1);
            coordinator.SetOutputMode(OutputMode.Monitor);
            await SettleAsync();
            Require(!mirrorClock.IsVisible && monitorClock.IsVisible && monitorClock.FontSize == 48,
                "Monitor clock has an independent size and Mirror clock stays hidden");
            output.VerifyTextFit();
            capture(output, Path.Combine(directory, "output-monitor-clock.png"), 1);

            foreach (OutputMode mode in Enum.GetValues<OutputMode>())
            {
                coordinator.SetOutputMode(mode);
                coordinator.SelectPage(1);
                viewModel.IsSlideFadeEnabled = false;
                await SettleAsync();
                coordinator.SelectPage(2);
                Require(!output.IsTransitionActive, "Disabled fade changes the page immediately");
                viewModel.SlideFadeMilliseconds = 1000;
                viewModel.IsSlideFadeEnabled = true;
                await SettleAsync();
                coordinator.SelectPage(1);
                Require(output.IsTransitionActive, "Enabled fade captures the outgoing page");
                await SettleAsync();
                capture(output, Path.Combine(directory, $"output-fade-{mode}.png"), 1);
                coordinator.SelectPage(3);
                Require(output.IsTransitionActive && coordinator.Snapshot.SelectedPageNumber == 3,
                    "Rapid navigation replaces the fade without queuing stale pages");
                coordinator.SetBlanked(true);
                Require(!output.IsTransitionActive, "Blank immediately discards an active fade");
                await SettleAsync();
                capture(output, Path.Combine(directory, $"output-fade-blank-{mode}.png"), 1);
                coordinator.SetBlanked(false);
                await SettleAsync();
                coordinator.SelectPage(1);
                Require(output.IsTransitionActive, "Fade works again after restore");
                coordinator.StopOutput();
                Require(!output.IsTransitionActive, "Stop immediately discards an active fade");
                coordinator.StartOutput();
                await SettleAsync();
                coordinator.SelectPage(2);
                viewModel.IsSlideFadeEnabled = false;
                Require(!output.IsTransitionActive, "Disabling fades removes the old frame immediately");
                viewModel.IsSlideFadeEnabled = true;
                await SettleAsync();
                coordinator.SelectPage(1);
                coordinator.SetDocument(imageDocument with { Title = "Replacement synthetic deck" });
                Require(!output.IsTransitionActive, "A deck replacement discards the outgoing deck frame");
                coordinator.SetDocument(imageDocument);
                viewModel.SlideFadeMilliseconds = 100;
                coordinator.SelectPage(1);
                await SettleAsync();
                coordinator.SelectPage(2);
                Require(output.IsTransitionActive, "Short fade begins");
                await Task.Delay(250);
                await SettleAsync();
                Require(!output.IsTransitionActive, "Completed fade releases its captured bitmap");
            }
            viewModel.ActiveSection = OperatorSection.SettingsDiagnostics;
            main.ScrollContentToTopForAutomation();
            ((FrameworkElement)main.FindName("OutputEffectsSettings")).BringIntoView();
            await SettleAsync();
            capture(main, Path.Combine(directory, "operator-output-effects-settings.png"), 1);
        }
        finally
        {
            coordinator.SetDocument(saved.Document);
            coordinator.SetAppearance(saved.Appearance);
            coordinator.SetOutputMode(saved.Mode);
            coordinator.SelectPage(saved.SelectedPageNumber);
            if (saved.IsLive) coordinator.StartOutput(); else coordinator.StopOutput();
            if (saved.IsBlanked) coordinator.SetBlanked(true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
