using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using CanvaStage.Windows.Services;
using CanvaStage.Windows.ViewModels;
using CanvaStage.Windows.Views;
using System.Windows;

namespace CanvaStage.Windows;

public partial class App : System.Windows.Application
{
    private OutputWindowManager? _outputWindowManager;

    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);

        var preferencesStore = new JsonPreferencesStore();
        AppPreferences preferences = preferencesStore.Load();
        var coordinator = new OutputCoordinator(DemoPresentationProvider.Create(), preferences);
        _outputWindowManager = new OutputWindowManager(coordinator);
        var displayService = new DisplayService();
        var credentialsStore = new CanvaCredentialsStore();
        var canva = new CanvaConnectService(credentialsStore);
        var viewModel = new MainViewModel(coordinator, _outputWindowManager, displayService, preferencesStore, canva, preferences);
        var mainWindow = new MainWindow(viewModel);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        _outputWindowManager?.Dispose();
        base.OnExit(eventArgs);
    }
}
