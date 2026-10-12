using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using Xunit;

namespace CanvaStage.Tests;

public sealed class SimpleSetupSelectionTests
{
    [Fact]
    public void UsesRememberedIdNotTitleOrFirstEntry()
    {
        CanvaPresentation expected = new("saved", "Renamed Sunday deck", 3);
        Assert.Equal(expected, SimpleSetupSelection.Resolve([new("other", "WCCF Sunday Slides", 4), expected], "saved"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("missing")]
    public void MissingDefaultDoesNotGuess(string? savedId)
        => Assert.Null(SimpleSetupSelection.Resolve([new("other", "WCCF Sunday Slides", 3)], savedId));

    [Fact]
    public void AmbiguousIdDoesNotGuess()
        => Assert.Null(SimpleSetupSelection.Resolve([new("same", "One", 3), new("same", "Two", 3)], "same"));

    [Fact]
    public void ReusesOnlyVerifiedMatchingIntactRenderedCache()
    {
        DateTimeOffset timestamp = DateTimeOffset.Parse("2026-10-11T12:00:00Z");
        PresentationDocument cached = new("saved", "Sunday", [new(1, "Page", ["Text"], "#000000", "#FFFFFF")], timestamp,
            SourceModifiedAt: timestamp, UsesRenderedPageImages: true);
        CanvaPresentation metadata = new("saved", "Sunday", 1, ModifiedAt: timestamp);
        Assert.True(SimpleSetupSelection.CanReuseCache(cached, metadata, true));
        Assert.False(SimpleSetupSelection.CanReuseCache(cached, metadata, false));
        Assert.False(SimpleSetupSelection.CanReuseCache(cached, metadata with { Id = "different" }, true));
        Assert.False(SimpleSetupSelection.CanReuseCache(cached, metadata with { ModifiedAt = timestamp.AddSeconds(1) }, true));
        Assert.False(SimpleSetupSelection.CanReuseCache(cached, metadata with { ModifiedAt = null }, true));
        Assert.False(SimpleSetupSelection.CanReuseCache(cached with { SourceModifiedAt = null }, metadata, true));
        Assert.False(SimpleSetupSelection.CanReuseCache(cached with { UsesRenderedPageImages = false }, metadata, true));
    }
}
