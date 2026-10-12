using CanvaStage.Windows.Services;
using Xunit;

namespace CanvaStage.Tests;

public sealed class WheelScrollPolicyTests
{
    [Theory]
    [InlineData(-120, 148)]
    [InlineData(-15, 106)]
    [InlineData(15, 94)]
    [InlineData(120, 52)]
    [InlineData(600, 0)]
    [InlineData(-6000, 500)]
    public void PreservesSmallDeltasAndClampsAtBounds(double delta, double expected)
        => Assert.Equal(expected, WheelScrollPolicy.Target(100, delta, 500, 3, 300));

    [Fact]
    public void RespectsDisabledAndPageScrollPreferences()
    {
        Assert.Equal(100, WheelScrollPolicy.Target(100, -120, 500, 0, 300));
        Assert.Equal(400, WheelScrollPolicy.Target(100, -120, 500, -1, 300));
    }
}
