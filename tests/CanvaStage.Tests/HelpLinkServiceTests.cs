using CanvaStage.Windows.Services;
using System.ComponentModel;
using Xunit;

namespace CanvaStage.Tests;

public sealed class HelpLinkServiceTests
{
    [Fact]
    public void OpensVersionedPublicGuideOnlyOnRequest()
    {
        Uri? opened = null;
        HelpLinkService service = new("0.12.0", uri => opened = uri);
        Assert.Null(opened);
        Assert.Null(service.TryOpen());
        Assert.Equal("https://mj00009.github.io/CanvaStage/v0.12.0/", opened?.AbsoluteUri);
        Assert.Equal(string.Empty, opened?.Query);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("0.13.0-preview")]
    [InlineData("../other?token=secret")]
    [InlineData("https://example.com/")]
    [InlineData("0.12.0\n")]
    [InlineData("01.12.0")]
    public void InvalidOrUnpublishedVersionUsesFixedLatestGuide(string version)
    {
        HelpLinkService service = new(version, _ => { });
        Assert.Equal("https://mj00009.github.io/CanvaStage/", service.Url.AbsoluteUri);
    }

    [Fact]
    public void BrowserFailureGivesSafeAddressWithoutExceptionDetails()
    {
        HelpLinkService service = new("0.12.0", _ => throw new Win32Exception("Private browser path"));
        string? message = service.TryOpen();
        Assert.Contains(service.Url.AbsoluteUri, message);
        Assert.Contains("internet access required", message);
        Assert.DoesNotContain("Private browser path", message);
    }
}
