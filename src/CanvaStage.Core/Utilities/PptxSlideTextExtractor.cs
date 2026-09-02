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

    public static IReadOnlyList<IReadOnlyList<string>> Extract(byte[] presentationFile)
    {
        using var stream = new MemoryStream(presentationFile);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        return archive.Entries
            .Where(IsSlideXml)
            .OrderBy(entry => GetSlideNumber(entry.FullName))
            .Select(ExtractSlideParagraphs)
            .ToList();
    }

    private static IReadOnlyList<string> ExtractSlideParagraphs(ZipArchiveEntry entry)
    {
        using Stream entryStream = entry.Open();
        XDocument document = XDocument.Load(entryStream);

        return document
            .Descendants(Drawing + "p")
            .Select(ExtractParagraphText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    private static string ExtractParagraphText(XElement paragraph) => string.Concat(
        paragraph.Descendants(Drawing + "t").Select(text => text.Value)).Trim();

    private static bool IsSlideXml(ZipArchiveEntry entry) =>
        entry.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) &&
        entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static int GetSlideNumber(string path)
    {
        string filename = Path.GetFileNameWithoutExtension(path);
        return int.TryParse(filename["slide".Length..], out int number) ? number : int.MaxValue;
    }
}
