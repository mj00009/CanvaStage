using CanvaStage.Core.Models;
using System.Text.Json;
using Xunit;

namespace CanvaStage.Tests;

public sealed class AppPreferencesTests
{
    [Fact]
    public void Default_EnablesAutomaticPresenterSync()
    {
        Assert.True(AppPreferences.Default.IsPresenterSyncEnabled);
    }

    [Fact]
    public void OlderPreferencesWithoutPresenterSetting_EnableAutomaticSync()
    {
        const string json = """
            {
              "SelectedDisplayId": null,
              "SelectedPage": 1,
              "OutputMode": 1,
              "MonitorLayout": 0,
              "NextLineOption": 0,
              "Appearance": {
                "BackgroundColor": "#101113",
                "TextColor": "#F7FDF5",
                "AccentColor": "#75E6C8",
                "FontFamily": "Aptos",
                "FontSize": 72,
                "ShowClock": false
              },
              "IsOperatorTopmost": false
            }
            """;

        AppPreferences? preferences = JsonSerializer.Deserialize<AppPreferences>(json);

        Assert.NotNull(preferences);
        Assert.True(preferences.IsPresenterSyncEnabled);
    }
}
