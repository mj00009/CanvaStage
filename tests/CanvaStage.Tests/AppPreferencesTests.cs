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
        Assert.True(preferences.IsAutomaticContentRefreshEnabled);
        Assert.Null(preferences.LastAppliedPresentationId);
        Assert.Equal(30, preferences.Appearance.NextLineFontSize);
        Assert.False(preferences.Appearance.IsNextLineBold);
        Assert.Equal("#F8F8FC", preferences.Appearance.NextLineColor);
    }

    [Fact]
    public void AutomaticContentRefreshOptOutSurvivesRestart()
    {
        AppPreferences expected = AppPreferences.Default with { IsAutomaticContentRefreshEnabled = false };
        Assert.False(JsonSerializer.Deserialize<AppPreferences>(JsonSerializer.Serialize(expected))!.IsAutomaticContentRefreshEnabled);
    }

    [Fact]
    public void LastAppliedDeckSurvivesRestartIndependentlyOfHighlightedSelection()
    {
        AppPreferences expected = AppPreferences.Default with
        {
            SelectedPresentationId = "highlighted-but-not-applied", LastAppliedPresentationId = "last-used",
            LastAppliedPresentationTitle = "Sunday"
        };
        Assert.Equal(expected, JsonSerializer.Deserialize<AppPreferences>(JsonSerializer.Serialize(expected)));
    }

    [Fact]
    public void NextLineAppearance_RoundTripsWithPreferences()
    {
        AppPreferences expected = AppPreferences.Default with
        {
            Appearance = MonitorAppearance.Default with
            {
                NextLineFontSize = 48,
                IsNextLineBold = true,
                NextLineColor = "#75E6C8"
            }
        };

        string json = JsonSerializer.Serialize(expected);
        AppPreferences? actual = JsonSerializer.Deserialize<AppPreferences>(json);

        Assert.Equal(expected, actual);
    }
}
