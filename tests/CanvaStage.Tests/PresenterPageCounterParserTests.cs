using CanvaStage.Core.Utilities;
using Xunit;

namespace CanvaStage.Tests;

public sealed class PresenterPageCounterParserTests
{
    [Theory]
    [InlineData("Page 8 of 292", 8, 292)]
    [InlineData("Slide 15 / 90", 15, 90)]
    [InlineData("31/120", 31, 120)]
    [InlineData("8 of 292", 8, 292)]
    public void ParsesSupportedPageCounters(string text, int expectedCurrent, int expectedTotal)
    {
        bool result = PresenterPageCounterParser.TryParse([text], out int currentPage, out int totalPages);

        Assert.True(result);
        Assert.Equal(expectedCurrent, currentPage);
        Assert.Equal(expectedTotal, totalPages);
    }

    [Fact]
    public void ParsesCounterSplitAcrossAccessibleElements()
    {
        bool result = PresenterPageCounterParser.TryParse(["Previous", "Page 8", "of 292", "Next"], out int currentPage, out int totalPages);

        Assert.True(result);
        Assert.Equal(8, currentPage);
        Assert.Equal(292, totalPages);
    }

    [Theory]
    [InlineData("3:45")]
    [InlineData("Copyright 2014")]
    [InlineData("Page 30 of 12")]
    public void RejectsTextThatIsNotASupportedPageCounter(string text)
    {
        Assert.False(PresenterPageCounterParser.TryParse([text], out _, out _));
    }
}
