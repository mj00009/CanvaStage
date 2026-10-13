using CanvaStage.Windows.Services;
using Xunit;

namespace CanvaStage.Tests;

public sealed class DisplayReconnectPolicyTests
{
    private static readonly DisplayOption Primary = new("laptop", "DISPLAY1", "Laptop", 1, 1920, 1080, true);
    private static readonly DisplayOption Receiver = new("receiver", "DISPLAY5", "Sample monitor", 2, 1920, 1080, false);

    [Fact]
    public void StartupDoesNotOfferEvenIfRememberedDisplayIsConnected()
        => Assert.Null(new DisplayReconnectPolicy().Observe([Primary, Receiver], Receiver.Id));

    [Fact]
    public void AttachingRememberedExtendedDisplayOffersOnce()
    {
        DisplayReconnectPolicy policy = new();
        Assert.Null(policy.Observe([Primary], Receiver.Id));
        Assert.Same(Receiver, policy.Observe([Primary, Receiver], Receiver.Id));
        Assert.Null(policy.Observe([Receiver, Primary], Receiver.Id));
        Assert.Null(policy.Observe([Primary, Receiver with { Number = 3 }], Receiver.Id));
    }

    [Fact]
    public void ReceiverReconnectCanUseNewDeviceNameWithoutGuessingAnotherDisplay()
    {
        DisplayReconnectPolicy policy = new();
        policy.Observe([Primary, Receiver], Receiver.Id);
        Assert.Null(policy.Observe([Primary], Receiver.Id));
        DisplayOption returned = Receiver with { ScreenDeviceName = "DISPLAY15" };
        Assert.Same(returned, policy.Observe([Primary, returned], Receiver.Id));
    }

    [Fact]
    public void ChoosingADifferentTargetDoesNotCountAsAReconnect()
    {
        DisplayReconnectPolicy policy = new();
        policy.Observe([Primary], Receiver.Id);
        DisplayOption other = Receiver with { Id = "other", ScreenDeviceName = "DISPLAY6" };
        Assert.Null(policy.Observe([Primary, other], other.Id));
    }

    [Fact]
    public void AmbiguousIdentityDoesNotOfferEvenWithAnOldDeviceMatch()
    {
        DisplayReconnectPolicy policy = new();
        policy.Observe([Primary], Receiver.Id);
        Assert.Null(policy.Observe([Primary, Receiver, Receiver with { ScreenDeviceName = "DISPLAY6" }], Receiver.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("missing")]
    [InlineData("laptop")]
    public void MissingUnknownOrPrimaryTargetsAreNeverOffered(string? rememberedId)
    {
        DisplayReconnectPolicy policy = new();
        policy.Observe([], rememberedId);
        Assert.Null(policy.Observe([Primary, Receiver], rememberedId));
        Assert.Null(DisplayReconnectPolicy.ResolveTarget([Primary, Receiver], rememberedId));
    }

    [Fact]
    public void LegacyDeviceNamesAreMatchedCaseInsensitively()
    {
        DisplayReconnectPolicy policy = new();
        policy.Observe([Primary], "display5");
        Assert.Same(Receiver, policy.Observe([Primary, Receiver], "display5"));
    }
}
