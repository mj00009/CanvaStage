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
