using System.IO.Compression;
using System.Xml.Linq;

namespace CanvaStage.Core.Utilities;

/// <summary>
/// Reads presentation text in the order it is authored, without introducing spaces
/// between adjacent PowerPoint text runs.
/// </summary>
public static class PptxSlideTextExtractor
{
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Presentation = "http://schemas.openxmlformats.org/presentationml/2006/main";

    public static IReadOnlyList<IReadOnlyList<string>> Extract(byte[] presentationFile)
        => ExtractSlides(presentationFile).Select(static slide => slide.TextBlocks).ToList();

    public static IReadOnlyList<PptxSlideContent> ExtractSlides(byte[] presentationFile)
    {
        using var stream = new MemoryStream(presentationFile);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        return archive.Entries
            .Where(IsSlideXml)
            .OrderBy(entry => GetSlideNumber(entry.FullName))
            .Select(ExtractSlide)
            .ToList();
    }

    private static PptxSlideContent ExtractSlide(ZipArchiveEntry entry)
    {
        using Stream entryStream = entry.Open();
        XDocument document = XDocument.Load(entryStream);
        IReadOnlyList<string> textBlocks = ExtractSlideParagraphs(document);
        bool isHidden = IsHidden(document.Root);
        return new PptxSlideContent(GetSlideNumber(entry.FullName), isHidden, textBlocks);
    }

    private static IReadOnlyList<string> ExtractSlideParagraphs(XDocument document)
    {
        var candidates = document
            .Descendants(Presentation + "sp")
            .Select(ExtractTextBlock)
            .Where(static block => block.Paragraphs.Count > 0)
            .ToList();

        if (candidates.Count == 0)
        {
            return document
                .Descendants(Drawing + "p")
                .Select(ExtractParagraphText)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
        }

        double largestFontSize = candidates.Max(static block => block.FontSize);
        double largestArea = candidates.Max(static block => block.Area);
        return candidates
            .OrderByDescending(block => GetMainTextScore(block, largestFontSize, largestArea))
            .ThenByDescending(static block => block.FontSize)
            .ThenByDescending(static block => block.Area)
            .First()
            .Paragraphs;
    }

    private static bool IsHidden(XElement? slide)
    {
        string? showValue = slide?.Attribute("show")?.Value;
        return string.Equals(showValue, "0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(showValue, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractParagraphText(XElement paragraph) => string.Concat(
        paragraph.Descendants().Where(element => element.Name == Drawing + "t" || element.Name == Drawing + "br")
            .Select(element => element.Name == Drawing + "br" ? "\n" : element.Value)).Trim();

    private static SlideTextBlock ExtractTextBlock(XElement shape)
    {
        XElement textBody = shape.Element(Presentation + "txBody") ?? shape;
        IReadOnlyList<string> paragraphs = textBody
            .Descendants(Drawing + "p")
            .Select(ExtractParagraphText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        double fontSize = textBody
            .Descendants()
            .Where(element => element.Name == Drawing + "rPr" || element.Name == Drawing + "defRPr")
            .Select(element => double.TryParse(element.Attribute("sz")?.Value, out double size) ? size / 100d : 0d)
            .Where(static size => size > 0d)
            .DefaultIfEmpty(18d)
            .Max();
        XElement? extent = shape.Element(Presentation + "spPr")?.Descendants(Drawing + "ext").FirstOrDefault();
        double width = double.TryParse(extent?.Attribute("cx")?.Value, out double parsedWidth) ? parsedWidth : 0d;
        double height = double.TryParse(extent?.Attribute("cy")?.Value, out double parsedHeight) ? parsedHeight : 0d;
        double area = width > 0d && height > 0d ? width * height : paragraphs.Sum(static text => text.Length) * fontSize;

        return new SlideTextBlock(paragraphs, fontSize, area);
    }

    private static double GetMainTextScore(SlideTextBlock block, double largestFontSize, double largestArea)
    {
        double fontScore = largestFontSize > 0d ? block.FontSize / largestFontSize : 0d;
        double areaScore = largestArea > 0d ? block.Area / largestArea : 0d;
        return (fontScore * 0.65d) + (areaScore * 0.35d);
    }

    private static bool IsSlideXml(ZipArchiveEntry entry) =>
        entry.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) &&
        entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static int GetSlideNumber(string path)
    {
        string filename = Path.GetFileNameWithoutExtension(path);
        return int.TryParse(filename["slide".Length..], out int number) ? number : int.MaxValue;
    }

    private sealed record SlideTextBlock(IReadOnlyList<string> Paragraphs, double FontSize, double Area);
}

public sealed record PptxSlideContent(int SourcePageNumber, bool IsHidden, IReadOnlyList<string> TextBlocks);
