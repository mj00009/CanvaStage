using CanvaStage.Windows.Services;
using Xunit;

namespace CanvaStage.Tests;

public sealed class PreviewLayoutTests
{
    [Theory]
    [InlineData(600, double.PositiveInfinity, 1920, 1080, 600, 337.5)]
    [InlineData(280, double.PositiveInfinity, 1920, 1080, 280, 157.5)]
    [InlineData(300, double.PositiveInfinity, 1080, 1920, 300, 533.333333333)]
    [InlineData(800, 300, 1920, 1080, 533.333333333, 300)]
    [InlineData(400, 400, 1080, 1080, 400, 400)]
    [InlineData(320, double.PositiveInfinity, 0, 0, 320, 180)]
    [InlineData(double.PositiveInfinity, double.PositiveInfinity, 1920, 1080, 640, 360)]
    [InlineData(0, 300, 1920, 1080, 0, 0)]
    public void PreviewFitsWholeSource(double availableWidth, double availableHeight,
        double sourceWidth, double sourceHeight, double expectedWidth, double expectedHeight)
    {
        (double width, double height) = PreviewLayout.Fit(availableWidth, availableHeight, sourceWidth, sourceHeight);
        Assert.Equal(expectedWidth, width, 6);
        Assert.Equal(expectedHeight, height, 6);
    }
}
