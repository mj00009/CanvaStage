using CanvaStage.Core.Models;
using System.Text.Json;
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

    [Fact]
    public void LegacyCacheWithoutImageSourceFlagIsNotTreatedAsAFreshRender()
    {
        const string json = """{"Id":"deck","Title":"Deck","Pages":[],"RefreshedAt":"2026-10-01T00:00:00Z"}""";
        Assert.False(JsonSerializer.Deserialize<PresentationDocument>(json)!.UsesRenderedPageImages);
    }

    [Fact]
    public void RenderedImageMarkerSurvivesCacheSerialization()
    {
        PresentationDocument document = new("deck", "Deck", [], DateTimeOffset.UtcNow, UsesRenderedPageImages: true);
        Assert.True(JsonSerializer.Deserialize<PresentationDocument>(JsonSerializer.Serialize(document))!.UsesRenderedPageImages);
    }
}
