using CanvaStage.Core.Utilities;
using Xunit;

namespace CanvaStage.Tests;

public sealed class SlideCapitalizationMatcherTests
{
    [Fact]
    public void MatchingVisibleTextChangesOnlyCasingAndKeepsPunctuationAndNames()
    {
        const string native = "EVERYONE CAN TRUST GOD, and visit the USA.\nKeep SampleText!";
        const string visible = "READING Everyone can trust God and visit the USA Keep SampleText FOOTER";
        Assert.Equal("Everyone can trust God, and visit the USA.\nKeep SampleText!",
            SlideCapitalizationMatcher.Match(native, [visible]));
    }

    [Theory]
    [InlineData("Everyone can trust God and visit the UK")]
    [InlineData("Everyone cannot trust God and visit the USA")]
    [InlineData("Everyone can trust God")]
    [InlineData("Everyone can trust God and quickly visit the USA")]
    public void DifferentOrIncompleteWordsCannotRewriteText(string reference) =>
        Assert.Equal("EVERYONE CAN TRUST GOD and visit the USA",
            SlideCapitalizationMatcher.Match("EVERYONE CAN TRUST GOD and visit the USA", [reference]));

    [Fact]
    public void ConflictingRepeatedMatchesLeaveOriginalAlone()
    {
        const string native = "EVERYONE CAN TRUST GOD and visit the USA";
        Assert.Equal(native, SlideCapitalizationMatcher.Match(native,
            ["Everyone can trust God and visit the USA", "Everyone can trust god and visit the USA"]));
    }

    [Fact]
    public void ConflictingOccurrencesOnOnePageAreAlsoAmbiguous()
    {
        const string native = "EVERYONE CAN TRUST GOD and visit the USA";
        Assert.Equal(native, SlideCapitalizationMatcher.Match(native,
            ["Everyone can trust God and visit the USA. EVERYONE CAN TRUST GOD and visit the USA."]));
    }

    [Theory]
    [InlineData("GOD")]
    [InlineData("IN THE USA")]
    public void ShortHeadingsAreNotGuessed(string native) =>
        Assert.Equal(native, SlideCapitalizationMatcher.Match(native, [native.ToLowerInvariant()]));

    [Fact]
    public void DifferentNumbersCannotBorrowCasingFromSimilarSlides() =>
        Assert.Equal("READ SAMPLE CHAPTER 12 WITH THE CLASS", SlideCapitalizationMatcher.Match(
            "READ SAMPLE CHAPTER 12 WITH THE CLASS", ["Read sample chapter 13 with the class"]));

    [Fact]
    public void VerifiedUppercaseRunCanBeCorrectedWithoutAcceptingAnOcrErrorElsewhere()
    {
        const string native = "EVERYONE SHOULD READ THIS SAMPLE TEXT with the whole group today. Keep the ending unchanged.";
        const string image = "Everyone should read this SampleText with the whole group today. Keep the ending unchanged.";
        Assert.Equal(native, SlideCapitalizationMatcher.Match(native, [image])); // Different token boundary.
        const string imperfect = "Everyone should read this sample text with the whole group today. Keep the endinq unchanged.";
        Assert.Equal("Everyone should read this sample text with the whole group today. Keep the ending unchanged.",
            SlideCapitalizationMatcher.Match(native, [imperfect]));
    }

    [Fact]
    public void ExistingMixedCaseNamesAndIsolatedAcronymsAreNotRewritten()
    {
        Assert.Equal("Everyone can trust God and visit the USA with SampleText.", SlideCapitalizationMatcher.Match(
            "EVERYONE CAN TRUST GOD and visit the USA with SampleText.",
            ["Everyone can trust God and visit the usa with sampletext."]));
    }

    [Fact]
    public void OriginalAndMatchedTextSurviveCacheRoundTripAndCanBeSwitchedOff()
    {
        CanvaStage.Core.Models.PresentationPage page = new(1, "SAMPLE TEXT FOR EVERYONE", ["SAMPLE TEXT FOR EVERYONE"], "#000000", "#FFFFFF",
            SlideCapitalizationTextBlocks: ["Sample text for everyone"], UseSlideCapitalization: true);
        var document = new CanvaStage.Core.Models.PresentationDocument("sample", "Sample", [page], DateTimeOffset.UtcNow, WasSlideCapitalizationChecked: true);
        string json = System.Text.Json.JsonSerializer.Serialize(document);
        var restored = System.Text.Json.JsonSerializer.Deserialize<CanvaStage.Core.Models.PresentationDocument>(json)!;
        Assert.Equal("Sample text for everyone", restored.Pages[0].PrimaryText);
        Assert.Equal("SAMPLE TEXT FOR EVERYONE", restored.WithSlideCapitalization(false).Pages[0].PrimaryText);
        Assert.Equal("Sample text for everyone", restored.WithSlideCapitalization(false).WithSlideCapitalization(true).Pages[0].PrimaryText);
        Assert.False(CanvaStage.Core.Models.AppPreferences.Default.MatchSlideCapitalization);
    }

    [Fact]
    public void NextCueAndTitleUseTheSameSelectedCasingAsMonitor()
    {
        const string raw = "SAMPLE TEXT FOR EVERYONE";
        const string matched = "Sample text for everyone";
        CanvaStage.Core.Models.PresentationPage first = new(1, "First", ["First"], "#000000", "#FFFFFF");
        CanvaStage.Core.Models.PresentationPage second = new(2, raw, [raw], "#000000", "#FFFFFF", SlideCapitalizationTextBlocks: [matched]);
        var document = new CanvaStage.Core.Models.PresentationDocument("sample", "Sample", [first, second], DateTimeOffset.UtcNow);
        var preferences = CanvaStage.Core.Models.AppPreferences.Default with { MatchSlideCapitalization = true };
        var coordinator = new CanvaStage.Core.Services.OutputCoordinator(document, preferences);
        Assert.Equal(matched, coordinator.Snapshot.NextLine);
        coordinator.SetNextLineOption(CanvaStage.Core.Models.NextLineOption.TitleOnly);
        Assert.Equal(matched, coordinator.Snapshot.NextLine);
        coordinator.SetDocument(coordinator.Snapshot.Document.WithSlideCapitalization(false));
        Assert.Equal(raw, coordinator.Snapshot.NextLine);
    }
}
