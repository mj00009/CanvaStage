using CanvaStage.Core.Services;
using Xunit;

namespace CanvaStage.Tests;

public sealed class ResilientPollingLoopTests
{
    [Fact]
    public async Task RetriesFailuresAndResetsBackoffAfterSuccessfulScan()
    {
        using CancellationTokenSource cancellation = new();
        List<double> retries = [];
        List<int> pages = [];
        int attempt = 0;
        await ResilientPollingLoop.RunAsync<int>(_ => ++attempt switch
        {
            1 or 2 or 4 => Task.FromException<int>(new InvalidOperationException("Window unavailable")),
            _ => Task.FromResult(attempt)
        }, page => { pages.Add(page); if (page == 5) cancellation.Cancel(); },
        (_, delay) => retries.Add(delay.TotalSeconds), cancellation.Token, (_, _) => Task.CompletedTask);
        Assert.Equal([1d, 2d, 1d], retries);
        Assert.Equal([3, 5], pages);
        Assert.Equal(15, ResilientPollingLoop.RetryDelay(100).TotalSeconds);
    }

    [Fact]
    public async Task CancelledScanCannotPublishStalePage()
    {
        using CancellationTokenSource cancellation = new();
        bool published = false;
        await ResilientPollingLoop.RunAsync(_ => { cancellation.Cancel(); return Task.FromResult(4); },
            _ => published = true, (_, _) => Assert.Fail("Cancellation is not a scan failure"), cancellation.Token);
        Assert.False(published);
    }
}
