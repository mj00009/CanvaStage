using CanvaStage.Core.Services;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;

namespace CanvaStage.Windows.Services;

/// <summary>Exercises dropdown reset, reconnect and actual Setup button commands.</summary>
internal static class DisplaySetupVerification
{
    public static async Task RunAsync(MainWindow main, MainViewModel model, IOutputCoordinator coordinator,
        string directory, Action<FrameworkElement, string, double> capture)
    {
        DisplayService service = new();
        IReadOnlyList<DisplayOption> actual = service.GetDisplays();
        DisplayOption original = model.SelectedDisplay ?? actual[0];
        bool originalIsLive = coordinator.Snapshot.IsLive;
        ComboBox selector = (ComboBox)main.FindName("SetupDisplaySelector");
        model.ActiveSection = OperatorSection.Setup;
        model.OutputManagerForVerification.StopOutput();
        DisplayOption receiver = new("verification-receiver", "VERIFICATION5", "Synthetic receiver", 2, 2880, 1800, false);
        try
        {
            model.ApplyDisplaySnapshot([original, receiver], "Verification attach");
            selector.SetCurrentValue(ComboBox.SelectedItemProperty, receiver);
            selector.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
            await Idle(main);
            Require(model.SelectedDisplay == receiver, "The Setup display selector does not update its selection");
            model.ApplyDisplaySnapshot([receiver with { Number = 1 }, original], "Verification reorder");
            await Idle(main);
            Require(model.SelectedDisplay?.Id == receiver.Id && ReferenceEquals(selector.SelectedItem, model.SelectedDisplay),
                "Rebuilding display items clears or changes the selected display");
            model.ApplyDisplaySnapshot([original], "Verification disconnect");
            await Idle(main);
            Require(model.SelectedDisplay is null && selector.SelectedItem is null && !model.StartOutputCommand.CanExecute(null),
                "Disconnect silently redirects output to the remaining display");
            model.ApplyDisplaySnapshot([original, receiver with { ScreenDeviceName = "VERIFICATION6" }], "Verification reconnect");
            await Idle(main);
            Require(model.SelectedDisplay?.ScreenDeviceName == "VERIFICATION6" && ReferenceEquals(selector.SelectedItem, model.SelectedDisplay)
                && !coordinator.Snapshot.IsLive, "Reconnect fails to restore the remembered target or starts output automatically");
            // Invoke the real display-change notification path, not just the list helper.
            model.OutputManagerForVerification.RefreshDisplayConfiguration();
            await Idle(main);
            Require(model.Displays.SequenceEqual(actual) && model.SelectedDisplay is null,
                "Windows display notifications do not refresh the UI safely");
            model.SelectedDisplay = actual.First(display => display.ScreenDeviceName == original.ScreenDeviceName);

            main.ScrollContentToEndForAutomation();
            await Idle(main);
            Invoke((Button)main.FindName("SetupIdentifyDisplaysButton"));
            await Idle(main);
            IReadOnlyList<DisplayIdentificationWindow> labels = model.DisplayIdentificationForVerification.WindowsForVerification;
            Require(labels.Count == actual.Count, "Identify displays did not create a label for every display");
            foreach (DisplayIdentificationWindow label in labels)
            {
                DisplayOptionViewModel data = (DisplayOptionViewModel)label.DataContext;
                DisplayOption display = model.Displays.Single(item => item.Number == data.Number);
                System.Windows.Forms.Screen screen = service.FindDisplay(display.ScreenDeviceName)!;
                Require(label.IsVisible && screen.Bounds.Contains(label.GetNativeBounds()),
                    "An identification label is positioned outside its physical display");
                capture(label, Path.Combine(directory, $"identify-display-{data.Number}.png"), 1);
            }
            model.DisplayIdentificationForVerification.Dispose();

            // Starting via Setup must work after the hidden shared preview was prepared,
            // without the old verification's explicit output.Show() shortcut.
            foreach (DisplayOption display in actual)
            {
                model.SelectedDisplay = display;
                await Idle(main);
                Invoke((Button)main.FindName("SetupStartOutputButton"));
                await Idle(main);
                OutputWindow output = model.OutputManagerForVerification.WindowForVerification;
                Require(coordinator.Snapshot.IsLive && output.IsVisible
                    && output.GetNativeBounds() == service.FindDisplay(display.ScreenDeviceName)!.Bounds,
                    "Setup Start output does not reach the selected physical display");
                model.OutputManagerForVerification.StopOutput();
                await Idle(main);
            }
        }
        finally
        {
            model.DisplayIdentificationForVerification.Dispose();
            model.OutputManagerForVerification.StopOutput();
            model.ApplyDisplaySnapshot(actual, "Verification restore");
            model.SelectedDisplay = original;
            model.ActiveSection = OperatorSection.LiveControl;
            main.ScrollContentToTopForAutomation();
            if (originalIsLive) coordinator.StartOutput();
        }
    }

    private static async Task Idle(Window window)
        => await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

    private static void Invoke(Button button)
    {
        Require(button.IsVisible && button.IsEnabled, "Setup display action is hidden or disabled");
        IInvokeProvider provider = (IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke);
        provider.Invoke();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
