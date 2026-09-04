using CanvaStage.Core.Models;
using Xunit;

namespace CanvaStage.Tests;

public sealed class PresentationDocumentTests
{
    [Fact]
    public void FindPageByExternalId_ReturnsMatchingPage()
    {
        var document = new PresentationDocument(
            "design-1",
            "Sunday Slides",
            [
                new PresentationPage(1, "First", ["First"], "#000000", "#FFFFFF", ExternalPageId: "page-1"),
                new PresentationPage(2, "Second", ["Second"], "#000000", "#FFFFFF", ExternalPageId: "page-2")
            ],
            DateTimeOffset.UtcNow);

        PresentationPage? result = document.FindPageByExternalId("page-2");

        Assert.NotNull(result);
        Assert.Equal(2, result.Number);
    }

    [Fact]
    public void FindPageByExternalId_ReturnsNullWhenPageIsNotCached()
    {
        var document = new PresentationDocument(
            "design-1",
            "Sunday Slides",
            [new PresentationPage(1, "First", ["First"], "#000000", "#FFFFFF", ExternalPageId: "page-1")],
            DateTimeOffset.UtcNow);

        Assert.Null(document.FindPageByExternalId("page-unknown"));
    }
}
