using CanvaStage.Core.Utilities;
using Xunit;

namespace CanvaStage.Tests;

public sealed class ContrastAnalyzerTests
{
    [Fact]
    public void DarkBackgroundAndWhiteTextMeetTheRecommendedContrast()
    {
        Assert.True(ContrastAnalyzer.HasRecommendedContrast("#F7F8FA", "#101113"));
    }

    [Fact]
    public void SimilarColorsProduceAWarningLevelContrast()
    {
        Assert.False(ContrastAnalyzer.HasRecommendedContrast("#252525", "#101113"));
    }

    [Fact]
    public void InvalidColorReturnsNoContrast()
    {
        Assert.Equal(0, ContrastAnalyzer.GetContrastRatio("invalid", "#101113"));
    }
}
