using CanvaStage.Core.Models;
using CanvaStage.Core.Services;
using Xunit;

namespace CanvaStage.Tests;

public sealed class OutputCoordinatorTests
{
    [Fact]
    public void StartsBlankButRetainsTheSavedPageSelection()
    {
        var coordinator = CreateCoordinator(selectedPage: 3);

        Assert.False(coordinator.Snapshot.IsLive);
        Assert.Null(coordinator.Snapshot.CurrentPage);
        Assert.Equal(3, coordinator.Snapshot.SelectedPageNumber);
    }

    [Fact]
    public void StartOutputPublishesTheSelectedPageAndImmediateNextPageLine()
    {
        var coordinator = CreateCoordinator();

        coordinator.StartOutput();

        Assert.True(coordinator.Snapshot.IsLive);
        Assert.Equal(1, coordinator.Snapshot.CurrentPage?.Number);
        Assert.Equal("Great is Your faithfulness", coordinator.Snapshot.NextLine);
    }

    [Fact]
    public void NavigationDoesNotWrapAtDeckBoundaries()
    {
        var coordinator = CreateCoordinator();

        coordinator.GoPrevious();
        Assert.Equal(1, coordinator.Snapshot.SelectedPageNumber);
        Assert.False(coordinator.Snapshot.CanGoPrevious);

        coordinator.SelectPage(5);
        coordinator.GoNext();
        Assert.Equal(5, coordinator.Snapshot.SelectedPageNumber);
        Assert.False(coordinator.Snapshot.CanGoNext);
    }

    [Fact]
    public void HiddenNextLineRemainsHiddenWhenOutputStarts()
    {
        var coordinator = CreateCoordinator(nextLineOption: NextLineOption.Hidden);

        coordinator.StartOutput();

        Assert.Null(coordinator.Snapshot.NextLine);
        Assert.Equal(NextLineOption.Hidden, coordinator.Snapshot.NextLineOption);
    }

    [Fact]
    public void BlankOutputHidesContentWithoutStoppingTheLiveSession()
    {
        var coordinator = CreateCoordinator();
        coordinator.StartOutput();

        coordinator.SetBlanked(true);

        Assert.True(coordinator.Snapshot.IsLive);
        Assert.True(coordinator.Snapshot.IsBlanked);
        Assert.Equal(1, coordinator.Snapshot.CurrentPage?.Number);

        coordinator.SetBlanked(false);
        Assert.False(coordinator.Snapshot.IsBlanked);
    }

    [Fact]
    public void ReplacingTheDocumentKeepsAValidPageAndPublishesTheNewContent()
    {
        var coordinator = CreateCoordinator(selectedPage: 3);
        var replacement = new PresentationDocument(
            "canva-123",
            "Sunday service",
            [
                new PresentationPage(1, "Welcome", ["Welcome home"], "#111111", "#FFFFFF"),
                new PresentationPage(2, "Prayer", ["Let us pray"], "#111111", "#FFFFFF")
            ],
            DateTimeOffset.UtcNow);

        coordinator.SetDocument(replacement);
        coordinator.StartOutput();

        Assert.Equal("canva-123", coordinator.Snapshot.Document.Id);
        Assert.Equal(1, coordinator.Snapshot.SelectedPageNumber);
        Assert.Equal("Welcome home", coordinator.Snapshot.CurrentPage?.PrimaryText);
    }

    [Fact]
    public void RefreshPublishesReentrantPresenterUpdatesInOrderToEverySubscriber()
    {
        OutputCoordinator coordinator = CreateCoordinator();
        coordinator.StartOutput();
        PresentationDocument replacement = DemoPresentationProvider.Create() with { Title = "Updated service" };
        coordinator.SnapshotChanged += (_, snapshot) =>
        {
            if (ReferenceEquals(snapshot.Document, replacement) && snapshot.SelectedPageNumber == 1)
                coordinator.SelectPage(3);
        };
        List<int> outputPages = [];
        coordinator.SnapshotChanged += (_, snapshot) => outputPages.Add(snapshot.SelectedPageNumber);

        coordinator.SetDocument(replacement);

        Assert.Equal([1, 3], outputPages);
        Assert.Equal(3, coordinator.Snapshot.SelectedPageNumber);
        coordinator.GoNext();
        Assert.Equal(4, outputPages[^1]);
    }

    [Fact]
    public void RefreshPreservesLiveBlankedStateAndStablePageIdentityAfterInsertion()
    {
        PresentationPage original = new(1, "Original", ["Original text"], "#000000", "#FFFFFF", ExternalPageId: "stable-page");
        OutputCoordinator coordinator = new(new("deck", "Deck", [original], DateTimeOffset.UtcNow), AppPreferences.Default);
        coordinator.StartOutput();
        coordinator.SetBlanked(true);
        PresentationDocument replacement = new("deck", "Updated", [
            original with { ExternalPageId = "inserted" },
            original with { Number = 2, TextBlocks = ["Updated text"] }], DateTimeOffset.UtcNow);

        coordinator.SetDocument(replacement);

        Assert.True(coordinator.Snapshot.IsLive);
        Assert.True(coordinator.Snapshot.IsBlanked);
        Assert.Equal(2, coordinator.Snapshot.SelectedPageNumber);
        Assert.Equal("Updated text", coordinator.Snapshot.CurrentPage?.PrimaryText);
    }

    [Fact]
    public void RefreshOfShorterDeckClampsSelectionToItsLastPage()
    {
        OutputCoordinator coordinator = CreateCoordinator(selectedPage: 5);
        coordinator.SetDocument(DemoPresentationProvider.Create() with { Pages = DemoPresentationProvider.Create().Pages.Take(3).ToArray() });
        Assert.Equal(3, coordinator.Snapshot.SelectedPageNumber);
    }

    [Fact]
    public void NextCueUsesOnlyTheFirstNonemptyAuthoredLine()
    {
        PresentationDocument document = DemoPresentationProvider.Create();
        PresentationPage[] pages = document.Pages.ToArray();
        pages[1] = pages[1] with { TextBlocks = ["\n   \r\nFirst line\nSecond line"] };
        OutputCoordinator coordinator = new(document with { Pages = pages }, AppPreferences.Default);
        Assert.Equal("First line", coordinator.Snapshot.NextLine);
    }

    private static OutputCoordinator CreateCoordinator(int selectedPage = 1, NextLineOption nextLineOption = NextLineOption.FirstNonEmptyLine)
    {
        AppPreferences preferences = AppPreferences.Default with
        {
            SelectedPage = selectedPage,
            NextLineOption = nextLineOption
        };
        return new OutputCoordinator(DemoPresentationProvider.Create(), preferences);
    }
}
