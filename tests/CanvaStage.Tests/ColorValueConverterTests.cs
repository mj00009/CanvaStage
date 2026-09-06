using CanvaStage.Core.Utilities;
using Xunit;

namespace CanvaStage.Tests;

public sealed class ColorValueConverterTests
{
    [Theory]
    [InlineData("#75e6c8", "#75E6C8")]
    [InlineData("232323", "#232323")]
    [InlineData("  #F7FDF5  ", "#F7FDF5")]
    public void TryNormalizeHex_AcceptsSixDigitColors(string input, string expected)
    {
        Assert.True(ColorValueConverter.TryNormalizeHex(input, out string normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#FFF")]
    [InlineData("#GG0000")]
    [InlineData("#12345678")]
    public void TryNormalizeHex_RejectsInvalidColors(string input)
    {
        Assert.False(ColorValueConverter.TryNormalizeHex(input, out _));
    }

    [Theory]
    [InlineData(0d, 1d, 1d, "#FF0000")]
    [InlineData(120d, 1d, 1d, "#00FF00")]
    [InlineData(240d, 1d, 1d, "#0000FF")]
    [InlineData(0d, 0d, 0.5d, "#808080")]
    public void HsvToHex_ConvertsSpectrumSelections(double hue, double saturation, double value, string expected)
    {
        Assert.Equal(expected, ColorValueConverter.HsvToHex(hue, saturation, value));
    }
}
