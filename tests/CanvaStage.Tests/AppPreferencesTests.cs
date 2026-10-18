using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
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
        Assert.False(preferences.Appearance.ShowMirrorClock);
        Assert.Equal(28, preferences.Appearance.ClockFontSize);
        Assert.Equal(28, preferences.Appearance.MirrorClockFontSize);
        Assert.True(preferences.Appearance.IsClockBold);
        Assert.False(preferences.Appearance.IsSlideFadeEnabled);
        Assert.Equal(250, preferences.Appearance.SlideFadeMilliseconds);
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

    [Fact]
    public void ClocksAndFadeSurviveRestart()
    {
        AppPreferences expected = AppPreferences.Default with
        {
            Appearance = MonitorAppearance.Default with
            {
                ShowMirrorClock = true, ClockFontSize = 48, MirrorClockFontSize = 64,
                IsClockBold = false, IsMirrorClockOnLeft = true,
                IsSlideFadeEnabled = true, SlideFadeMilliseconds = 600
            }
        };
        Assert.Equal(expected, JsonSerializer.Deserialize<AppPreferences>(JsonSerializer.Serialize(expected)));
    }

    [Theory]
    [InlineData(-1, 16, 100)]
    [InlineData(10000, 96, 1000)]
    [InlineData(double.NaN, 28, 250)]
    [InlineData(double.PositiveInfinity, 28, 250)]
    public void UnsafeClockAndFadeValuesAreNormalized(double input, double size, double duration)
    {
        MonitorAppearance actual = (MonitorAppearance.Default with
        {
            ClockFontSize = input, MirrorClockFontSize = input, SlideFadeMilliseconds = input
        }).Normalize();
        Assert.Equal(size, actual.ClockFontSize);
        Assert.Equal(size, actual.MirrorClockFontSize);
        Assert.Equal(duration, actual.SlideFadeMilliseconds);
        OutputCoordinator coordinator = new(DemoPresentationProvider.Create(), AppPreferences.Default with
        {
            Appearance = MonitorAppearance.Default with { ClockFontSize = input, MirrorClockFontSize = input, SlideFadeMilliseconds = input }
        });
        Assert.Equal(actual, coordinator.Snapshot.Appearance);
        coordinator.SetAppearance(MonitorAppearance.Default with { ClockFontSize = input, MirrorClockFontSize = input, SlideFadeMilliseconds = input });
        Assert.Equal(actual, coordinator.Snapshot.Appearance);
    }
}
