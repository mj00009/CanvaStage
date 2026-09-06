using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Windows.Services;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CanvaStage.Windows;

public partial class App : System.Windows.Application
{
    private OutputWindowManager? _outputWindowManager;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        PrivacySafeLog.Write("Startup: application initialization began.");
        try
        {
            base.OnStartup(eventArgs);

            var preferencesStore = new JsonPreferencesStore();
            AppPreferences preferences = preferencesStore.Load();
            PrivacySafeLog.Write("Startup: preferences loaded.");

            var presentationCacheStore = new PresentationCacheStore();
            PresentationDocument initialDocument = presentationCacheStore.Load() ?? DemoPresentationProvider.Create();
            var coordinator = new OutputCoordinator(initialDocument, preferences);
            _outputWindowManager = new OutputWindowManager(coordinator);
            var displayService = new DisplayService();
            var credentialsStore = new CanvaCredentialsStore();
            var canva = new CanvaConnectService(credentialsStore);
            var canvaPresenterAccessibilityTracker = new CanvaPresenterAccessibilityTracker();
            var viewModel = new MainViewModel(coordinator, _outputWindowManager, displayService, preferencesStore, presentationCacheStore, canva, canvaPresenterAccessibilityTracker, preferences);
            PrivacySafeLog.Write("Startup: services created.");

            var mainWindow = new MainWindow(viewModel);
            MainWindow = mainWindow;
            mainWindow.Show();
            PrivacySafeLog.Write("Startup: main window shown.");

            string? screenshotPath = GetUiDemoScreenshotPath(eventArgs.Args);
            if (screenshotPath is not null)
            {
                string? colorPickerScreenshotPath = GetArgumentValue(eventArgs.Args, "--ui-color-picker-screenshot=");
                PrivacySafeLog.Write("Startup: UI demo screenshot mode started.");
                Dispatcher.BeginInvoke(
                    () => CaptureUiDemoScreenshots(mainWindow, screenshotPath, colorPickerScreenshotPath),
                    DispatcherPriority.ApplicationIdle);
            }
            else if (eventArgs.Args.Contains("--smoke-test", StringComparer.Ordinal))
            {
                PrivacySafeLog.Write("Startup: smoke-test mode passed; closing application.");
                Dispatcher.BeginInvoke(Shutdown, DispatcherPriority.ApplicationIdle);
            }
        }
        catch (Exception exception)
        {
            PrivacySafeLog.WriteException("Startup failure", exception);
            System.Windows.MessageBox.Show(
                "CanvaStage could not start. Details were saved to %LOCALAPPDATA%\\CanvaStage\\canvastage.log.",
                "CanvaStage startup problem",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        _outputWindowManager?.Dispose();
        PrivacySafeLog.Write($"Application exited with code {eventArgs.ApplicationExitCode}.");
        base.OnExit(eventArgs);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        PrivacySafeLog.WriteException("Unhandled WPF dispatcher exception", eventArgs.Exception);
        eventArgs.Handled = true;
        System.Windows.MessageBox.Show(
            "CanvaStage encountered an unexpected problem. Details were saved to %LOCALAPPDATA%\\CanvaStage\\canvastage.log.",
            "CanvaStage problem",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs eventArgs)
    {
        PrivacySafeLog.WriteException("Unhandled application-domain exception", eventArgs.ExceptionObject as Exception ?? new InvalidOperationException("An unknown application-domain exception occurred."));
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        PrivacySafeLog.WriteException("Unobserved task exception", eventArgs.Exception);
        eventArgs.SetObserved();
    }

    private static string? GetUiDemoScreenshotPath(IEnumerable<string> arguments)
        => GetArgumentValue(arguments, "--ui-demo-screenshot=");

    private static string? GetArgumentValue(IEnumerable<string> arguments, string argumentPrefix)
    {
        string? argument = arguments.FirstOrDefault(value => value.StartsWith(argumentPrefix, StringComparison.OrdinalIgnoreCase));
        return argument is null ? null : argument[argumentPrefix.Length..];
    }

    private void CaptureUiDemoScreenshots(MainWindow mainWindow, string screenshotPath, string? colorPickerScreenshotPath)
    {
        try
        {
            SaveVisualScreenshot(mainWindow, screenshotPath);
            if (!string.IsNullOrWhiteSpace(colorPickerScreenshotPath))
            {
                var colorPicker = new ColorPickerWindow("#8454F4") { Owner = mainWindow };
                colorPicker.Show();
                SaveVisualScreenshot(colorPicker, colorPickerScreenshotPath);
                colorPicker.Close();
            }

            PrivacySafeLog.Write("Startup: UI demo screenshots were created.");
            Shutdown();
        }
        catch (Exception exception)
        {
            PrivacySafeLog.WriteException("UI demo screenshot failure", exception);
            Shutdown(-1);
        }
    }

    private static void SaveVisualScreenshot(FrameworkElement visual, string screenshotPath)
    {
        visual.UpdateLayout();
        int width = Math.Max(1, (int)Math.Ceiling(visual.ActualWidth));
        int height = Math.Max(1, (int)Math.Ceiling(visual.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        string? directory = Path.GetDirectoryName(screenshotPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(screenshotPath);
        encoder.Save(output);
    }
}
