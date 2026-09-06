using CanvaStage.Core.Utilities;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace CanvaStage.Tests;

public sealed class PptxSlideTextExtractorTests
{
    [Fact]
    public void Extract_PreservesWordsSplitAcrossAdjacentPowerPointRuns()
    {
        byte[] presentation = CreatePresentationWithSlides((1,
            "<a:p><a:r><a:t>won't w</a:t></a:r><a:r><a:t>aste another minute</a:t></a:r></a:p>" +
            "<a:p><a:r><a:t>Praise the Lord</a:t></a:r></a:p>"));

        IReadOnlyList<IReadOnlyList<string>> slides = PptxSlideTextExtractor.Extract(presentation);

        Assert.Equal(["won't waste another minute", "Praise the Lord"], slides[0]);
    }

    [Fact]
    public void Extract_UsesSlideNumberOrdering()
    {
        IReadOnlyList<IReadOnlyList<string>> slides = PptxSlideTextExtractor.Extract(CreatePresentationWithSlides(
            (2, "<a:p><a:r><a:t>Second</a:t></a:r></a:p>"),
            (1, "<a:p><a:r><a:t>First</a:t></a:r></a:p>")));

        Assert.Equal("First", slides[0][0]);
        Assert.Equal("Second", slides[1][0]);
    }

    [Fact]
    public void Extract_SelectsTheLargestMainTextBlockInsteadOfFooterText()
    {
        const string footer = "<p:sp><p:spPr><a:xfrm><a:ext cx=\"1800000\" cy=\"250000\" /></a:xfrm></p:spPr><p:txBody><a:p><a:r><a:rPr sz=\"1200\" /><a:t>© 2014 Be Essential Songs</a:t></a:r></a:p></p:txBody></p:sp>";
        const string main = "<p:sp><p:spPr><a:xfrm><a:ext cx=\"6000000\" cy=\"3000000\" /></a:xfrm></p:spPr><p:txBody><a:p><a:r><a:rPr sz=\"3200\" /><a:t>Unstoppable God</a:t></a:r></a:p><a:p><a:r><a:rPr sz=\"3200\" /><a:t>Let your glory go on and on</a:t></a:r></a:p></p:txBody></p:sp>";

        IReadOnlyList<IReadOnlyList<string>> slides = PptxSlideTextExtractor.Extract(CreatePresentationWithSlides((1, footer + main)));

        Assert.Equal(["Unstoppable God", "Let your glory go on and on"], slides[0]);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("false")]
    public void ExtractSlides_IdentifiesHiddenSlides(string showValue)
    {
        byte[] presentation = CreatePresentationWithSlides(
            (1, "<a:p><a:r><a:t>Visible</a:t></a:r></a:p>", null),
            (2, "<a:p><a:r><a:t>Hidden</a:t></a:r></a:p>", showValue));

        IReadOnlyList<PptxSlideContent> slides = PptxSlideTextExtractor.ExtractSlides(presentation);

        Assert.False(slides[0].IsHidden);
        Assert.True(slides[1].IsHidden);
        Assert.Equal(2, slides[1].SourcePageNumber);
    }

    [Fact]
    public void ExtractSlides_TreatsMissingShowAttributeAsVisible()
    {
        IReadOnlyList<PptxSlideContent> slides = PptxSlideTextExtractor.ExtractSlides(
            CreatePresentationWithSlides((1, "<a:p><a:r><a:t>Visible</a:t></a:r></a:p>", null)));

        Assert.False(slides[0].IsHidden);
    }

    [Fact]
    public void VisibleSlides_PreserveTheirOriginalPageNumbers()
    {
        IReadOnlyList<PptxSlideContent> slides = PptxSlideTextExtractor.ExtractSlides(CreatePresentationWithSlides(
            (1, "<a:p><a:r><a:t>First</a:t></a:r></a:p>", null),
            (2, "<a:p><a:r><a:t>Hidden</a:t></a:r></a:p>", "0"),
            (3, "<a:p><a:r><a:t>Third</a:t></a:r></a:p>", null)));

        int[] visibleSourcePageNumbers = slides
            .Where(static slide => !slide.IsHidden)
            .Select(static slide => slide.SourcePageNumber)
            .ToArray();

        Assert.Equal([1, 3], visibleSourcePageNumbers);
    }

    private static byte[] CreatePresentationWithSlides(params (int Number, string Paragraphs)[] slides) =>
        CreatePresentationWithSlides(slides.Select(static slide => (slide.Number, slide.Paragraphs, (string?)null)).ToArray());

    private static byte[] CreatePresentationWithSlides(params (int Number, string Paragraphs, string? ShowValue)[] slides)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((int number, string paragraphs, string? showValue) in slides)
            {
                ZipArchiveEntry entry = archive.CreateEntry($"ppt/slides/slide{number}.xml");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                string showAttribute = showValue is null ? string.Empty : $" show=\"{showValue}\"";
                writer.Write($"<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"{showAttribute}><p:cSld><p:spTree>{paragraphs}</p:spTree></p:cSld></p:sld>");
            }
        }

        return memory.ToArray();
    }
}
