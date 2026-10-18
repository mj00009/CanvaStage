using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;

namespace CanvaStage.Windows.Services;

internal static class DisplayMirrorVerification
{
    public static async Task RunAsync(string directory, Action<FrameworkElement, string, double> capture)
    {
        // Read native CCD data/ABI on Windows; every mutation below uses a fake backend.
        DisplayTopology native = new WindowsDisplayTopology().Capture();
        Require(native.Paths.Length > 0 && native.Paths.All(path => native.SourceMode(path).Width > 0), "Native Windows topology/source mode query failed");
        string storage = Path.Combine(directory, "isolated-storage", "display-mirror");
        Backend backend = new();
        DisplayMirrorService layouts = new(storage, backend);
        DisplayOption[] displays = Enumerable.Range(1, 3).Select(index => new DisplayOption($"screen-{index}", $"SCREEN{index}", index == 2 ? "Synthetic audience" : index == 3 ? "Synthetic confidence" : "Synthetic operator", index, 1920, 1080, index == 1)).ToArray();
        DisplayService service = new();
        OutputCoordinator coordinator = new(DemoPresentationProvider.Create(), AppPreferences.Default);
        OutputWindowManager manager = new(coordinator);
        using HttpClient client = new();
        CanvaConnectService canva = new(new CanvaCredentialsStore(storage), client, Path.Combine(storage, "cache"));
        bool answer = false;
        bool keep = true;
        string? lastPrompt = null;
        Action? duringConfirmation = null;
        MainViewModel model = new(coordinator, manager, service, new DisplayIdentificationService(service), new DiagnosticsExportService(),
            new JsonPreferencesStore(storage), new PresentationCacheStore(Path.Combine(storage, "cache")), canva,
            new WindowsReleaseVerification.DemoTracker(), AppPreferences.Default,
            getDisplays: () => backend.IsCloned ? displays[..2] : displays, displayMirror: layouts,
            confirmLayoutChange: message => { lastPrompt = message; duringConfirmation?.Invoke(); return answer; },
            keepMirroredLayout: () => keep);
        MainWindow main = new(model);
        try
        {
            main.Show();
            VerificationWindowSizing.Configure(main);
            VerificationWindowSizing.SetSize(main, 1380, 900);
            model.ActiveSection = OperatorSection.Setup;
            model.IsAdvancedSetup = true;
            model.SelectedDisplay = displays[2];
            main.ScrollContentToTopForAutomation();
            await main.Dispatcher.InvokeAsync(() => main.UpdateLayout(), DispatcherPriority.ApplicationIdle);
            Require(model.MirrorAudienceDisplay?.ScreenDeviceName == "SCREEN2", "Fallback does not pick the only other Extended screen");
            Button button = (Button)main.FindName("MirrorAudienceButton");
            Require(button.IsVisible && ReferenceEquals(button.Command, model.MirrorAudienceCommand), "Advanced mirror button is missing or unbound");
            FrameworkElement card = (FrameworkElement)main.FindName("MirrorFallbackCard");
            Require(card.TransformToAncestor((FrameworkElement)main.FindName("AdvancedSetupPanel")).Transform(new System.Windows.Point()).Y < 1,
                "Mirror fallback is not at the top of Advanced Setup");
            capture(main, Path.Combine(directory, "setup-mirror-fallback.png"), 1);
            coordinator.StartOutput();
            await model.ChangeDisplayLayoutAsync(false);
            Require(coordinator.Snapshot.IsLive && backend.Applies == 0 && !layouts.HasRestorePoint,
                "No confirmation changes the layout or stops output");
            Require(lastPrompt is not null && lastPrompt.Contains("Synthetic audience") && lastPrompt.Contains("Synthetic confidence")
                && lastPrompt.Contains("Are you sure") && lastPrompt.Contains("primary operator"), "Confirmation does not explain the selected screens and effect");
            answer = true;
            duringConfirmation = () => backend.Current.Modes[2].X += 10;
            await model.ChangeDisplayLayoutAsync(false);
            Require(backend.Applies == 0 && coordinator.Snapshot.IsLive, "Stale layout confirmation changes screens or stops output");
            duringConfirmation = null;
            backend.Current = Backend.Sample();
            keep = false;
            await model.ChangeDisplayLayoutAsync(false);
            Require(!backend.IsCloned && !layouts.HasRestorePoint && !coordinator.Snapshot.IsLive, "Declined keep does not restore Extend and keep output stopped");
            keep = true;
            await model.ChangeDisplayLayoutAsync(false);
            Require(backend.IsCloned && layouts.HasRestorePoint && !coordinator.Snapshot.IsLive && !model.StartOutputCommand.CanExecute(null)
                && !model.StartSimpleSetupCommand.CanExecute(null), "Mirroring permits confidence output to cover the shared audience desktop");
            Require(model.MirrorFallbackTarget.StartsWith("Restore the saved layout"), "Active fallback asks for an unavailable separate screen");
            await model.StartSimpleSetupAsync();
            Require(!coordinator.Snapshot.IsLive, "Simple Setup bypasses active mirroring guard");
            await main.Dispatcher.InvokeAsync(() => main.UpdateLayout(), DispatcherPriority.ApplicationIdle);
            capture(main, Path.Combine(directory, "setup-mirror-fallback-active.png"), 1);
            answer = false;
            await model.ChangeDisplayLayoutAsync(true);
            Require(backend.IsCloned, "No to restoring still changes Windows");
            answer = true;
            await model.ChangeDisplayLayoutAsync(true);
            Require(!backend.IsCloned && !layouts.HasRestorePoint && !coordinator.Snapshot.IsLive && model.SelectedDisplay?.ScreenDeviceName == "SCREEN3",
                "Restore does not preserve the selected confidence screen and keep output stopped");

            // Test the real countdown dialog, not the display driver. No is the default;
            // a shortened timeout must return false without an injected button answer.
            KeepDisplayLayoutWindow dialog = new(TimeSpan.FromMilliseconds(80));
            dialog.Loaded += (_, _) => capture(dialog, Path.Combine(directory, "keep-mirror-layout-dialog.png"), 1);
            Require(dialog.ShowDialog() != true, "The keep-layout timeout accepts a layout without consent");
        }
        finally { coordinator.StopOutput(); main.Close(); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed class Backend : IDisplayTopologyBackend
    {
        public DisplayTopology Current = Sample();
        public int Applies;
        public bool IsCloned => Current.SourceMode(Current.Paths[1]).X == Current.SourceMode(Current.Paths[2]).X;
        public DisplayTopology Capture() => new((DisplayPath[])Current.Paths.Clone(), (DisplayMode[])Current.Modes.Clone(), (DisplayEndpoint[])Current.Endpoints.Clone());
        public void Validate(DisplayTopology topology) { }
        public void Apply(DisplayTopology topology) { Applies++; Current = topology; }
        public static DisplayTopology Sample()
        {
            DisplayLuid adapter = new() { Low = 1 };
            DisplayPath[] paths = Enumerable.Range(0, 3).Select(index => new DisplayPath
            {
                Source = new() { Adapter = adapter, Id = (uint)index, ModeIndex = ((uint)index << 16) | 0xFFFF },
                Target = new() { Adapter = adapter, Id = (uint)(index + 10), ModeIndex = uint.MaxValue, Available = 1, Rotation = 1 }, Flags = 9
            }).ToArray();
            DisplayMode[] modes = Enumerable.Range(0, 3).Select(index => new DisplayMode
            { Type = 1, Adapter = adapter, Id = (uint)index, Width = 1920, Height = 1080, PixelFormat = 4, X = index * 1920 }).ToArray();
            return new(paths, modes, paths.Select((path, index) => new DisplayEndpoint(DisplayTopology.TargetKey(path), $"SCREEN{index + 1}", $"synthetic-monitor-{index}")).ToArray());
        }
    }
}
