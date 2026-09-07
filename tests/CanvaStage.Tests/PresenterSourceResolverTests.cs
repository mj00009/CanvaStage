using CanvaStage.Core.Services;
using Xunit;

namespace CanvaStage.Tests;

public sealed class PresenterSourceResolverTests
{
    private static PresenterSource Source(string key = "one", string title = "Sunday - Canva - Google Chrome", string? id = null, int page = 2, int total = 91)
        => new(key, title, "Chrome", id, page, total);
    private static PresenterSyncDecision Resolve(params PresenterSource[] sources)
        => PresenterSourceResolver.Resolve(sources, "DESIGN1", "Sunday", 91, null);

    [Fact] public void SameCountAloneRequiresConfirmation() => Assert.Equal(PresenterSyncState.ChooseWindow, Resolve(Source(title: "Presenter view")).State);
    [Fact] public void ExactTitleMatchesBrowserSuffix() => Assert.Equal(PresenterSyncState.Following, Resolve(Source()).State);
    [Fact] public void SimilarTitleDoesNotMatch() => Assert.Equal(PresenterSyncState.ChooseWindow, Resolve(Source(title: "Sunday rehearsal - Canva")).State);
    [Fact] public void DesignIdentityOverridesMatchingTitle() => Assert.Equal(PresenterSyncState.Mismatch, Resolve(Source(id: "OTHER")).State);
    [Fact] public void VerifiedDesignDoesNotNeedTitle() => Assert.Equal(PresenterSyncState.Following, Resolve(Source(title: "Presenter", id: "DESIGN1")).State);
    [Fact] public void DuplicatePresentersRequireChoice() => Assert.Equal(PresenterSyncState.ChooseWindow, Resolve(Source(), Source(key: "two")).State);
    [Fact] public void ExplicitChoiceSelectsCorrectWindow()
    {
        PresenterSyncDecision result = PresenterSourceResolver.Resolve([Source(), Source(key: "two", page: 8)], "DESIGN1", "Sunday", 91, "two");
        Assert.Equal(8, result.Source?.CurrentPage);
    }
    [Fact] public void ConfirmationCannotOverrideKnownWrongDesign()
        => Assert.Equal(PresenterSyncState.Mismatch, PresenterSourceResolver.Resolve([Source(id: "OTHER")], "DESIGN1", "Sunday", 91, "one").State);
    [Fact] public void MissingChosenWindowDoesNotSwitchToAnother()
        => Assert.Equal(PresenterSyncState.Waiting, PresenterSourceResolver.Resolve([Source(key: "two")], "DESIGN1", "Sunday", 91, "one").State);
    [Fact] public void PageCountMismatchBlocksFollowing() => Assert.Equal(PresenterSyncState.Mismatch, Resolve(Source(total: 319)).State);
    [Fact] public void OutOfRangePageBlocksFollowing() => Assert.Equal(PresenterSyncState.Mismatch, Resolve(Source(page: 92)).State);
    [Fact] public void DemoCannotFollowCanva() => Assert.Equal(PresenterSyncState.Waiting, PresenterSourceResolver.Resolve([Source()], "demo-sunday", "Sunday", 91, "one").State);

    [Theory]
    [InlineData("https://www.canva.com/design/DA123/edit", "DA123")]
    [InlineData("https://canva.com/design/DA123/view?token=ignored", "DA123")]
    [InlineData("https://canva.com.attacker.test/design/DA123/view", null)]
    [InlineData("https://www.canva.com/oauth/authorize?code=ignored", null)]
    [InlineData("http://www.canva.com/design/DA123/view", null)]
    [InlineData("not a url", null)]
    public void OnlyExtractsDesignIdFromCanvaDocumentUrls(string url, string? expected)
        => Assert.Equal(expected, PresenterSourceResolver.GetDesignId(url));
}
