using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Windows.Services;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using System.Windows;
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

            var coordinator = new OutputCoordinator(DemoPresentationProvider.Create(), preferences);
            _outputWindowManager = new OutputWindowManager(coordinator);
            var displayService = new DisplayService();
            var credentialsStore = new CanvaCredentialsStore();
            var canva = new CanvaConnectService(credentialsStore);
            var viewModel = new MainViewModel(coordinator, _outputWindowManager, displayService, preferencesStore, canva, preferences);
            PrivacySafeLog.Write("Startup: services created.");

            var mainWindow = new MainWindow(viewModel);
            MainWindow = mainWindow;
            mainWindow.Show();
            PrivacySafeLog.Write("Startup: main window shown.");

            if (eventArgs.Args.Contains("--smoke-test", StringComparer.Ordinal))
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
}
