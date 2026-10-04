using CanvaStage.Windows.Services;
using System.Drawing;
using Xunit;

namespace CanvaStage.Tests;

public sealed class DisplaySelectionTests
{
    private static readonly DisplayOption Primary = new("internal", "DISPLAY1", "Laptop", 2, 3456, 2160, true);
    private static readonly DisplayOption Extended = new("receiver", "DISPLAY5", "Monitor", 1, 2880, 1800, false);

    [Fact]
    public void RefreshPreservesChosenDisplayWhenOrderAndNumberChange()
    {
        DisplayOption reordered = Extended with { Number = 2 };
        Assert.Same(reordered, DisplaySelection.Resolve([Primary, reordered], Extended.Id, Extended.ScreenDeviceName));
    }

    [Fact]
    public void DisconnectDoesNotFallBackToPrimary()
        => Assert.Null(DisplaySelection.Resolve([Primary], Extended.Id, Extended.ScreenDeviceName));

    [Fact]
    public void ReconnectRestoresIdentityDespiteNewWindowsDeviceName()
    {
        DisplayOption reconnected = Extended with { ScreenDeviceName = "DISPLAY6" };
        Assert.Same(reconnected, DisplaySelection.Resolve([Primary, reconnected], Extended.Id, Extended.ScreenDeviceName));
    }

    [Fact]
    public void UnavailableSavedDeviceDoesNotSelectTheFirstAvailableMonitor()
        => Assert.Null(DisplaySelection.Resolve([Primary, Extended], "DISPLAY14"));

    [Fact]
    public void LegacyCurrentWindowsDeviceNameStillResolves()
        => Assert.Same(Extended, DisplaySelection.Resolve([Primary, Extended], "display5"));

    [Fact]
    public void DuplicateSavedIdentitiesRequireAnExactCurrentDevice()
    {
        DisplayOption duplicate = Extended with { ScreenDeviceName = "DISPLAY6" };
        Assert.Null(DisplaySelection.Resolve([Extended, duplicate], Extended.Id));
        Assert.Same(duplicate, DisplaySelection.Resolve([Extended, duplicate], Extended.Id, "display6"));
    }

    [Fact]
    public void FirstRunWithoutASavedChoiceRetainsDefaultSelection()
        => Assert.Same(Primary, DisplaySelection.Resolve([Primary, Extended], null));

    [Theory]
    [InlineData(0, 0, 3456, 2160, 1)]
    [InlineData(0, 0, 3456, 2160, 2.5)]
    [InlineData(-2880, 0, 2880, 1800, 1.25)]
    [InlineData(-2880, 0, 2880, 1800, 2.5)]
    [InlineData(0, -1080, 1920, 1080, 1.5)]
    [InlineData(3456, 0, 1280, 720, 3)]
    [InlineData(-400, -300, 400, 300, 2.5)]
    public void IdentificationBoundsStayInsidePhysicalDisplay(int left, int top, int width, int height, double scale)
    {
        Rectangle monitor = new(left, top, width, height);
        Rectangle label = DisplayIdentificationPlacement.GetBounds(monitor, scale);
        Assert.True(monitor.Contains(label));
        Assert.InRange(Math.Abs(label.Left + label.Width / 2d - (monitor.Left + monitor.Width / 2d)), 0, 1);
        Assert.InRange(Math.Abs(label.Top + label.Height / 2d - (monitor.Top + monitor.Height / 2d)), 0, 1);
    }

    [Fact]
    public void IdentificationSizeScalesButDesktopOriginDoesNot()
    {
        Rectangle label = DisplayIdentificationPlacement.GetBounds(new Rectangle(-2880, 0, 2880, 1800), 2.5);
        Assert.Equal(new Rectangle(-1890, 550, 900, 700), label);
    }
}
