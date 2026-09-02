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

    private static byte[] CreatePresentationWithSlides(params (int Number, string Paragraphs)[] slides)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((int number, string paragraphs) in slides)
            {
                ZipArchiveEntry entry = archive.CreateEntry($"ppt/slides/slide{number}.xml");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write($"<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><p:cSld><p:spTree>{paragraphs}</p:spTree></p:cSld></p:sld>");
            }
        }

        return memory.ToArray();
    }
}
