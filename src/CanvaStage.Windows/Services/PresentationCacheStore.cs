using CanvaStage.Core.Models;
using System.IO;
using System.Text.Json;

namespace CanvaStage.Windows.Services;

public sealed class PresentationCacheStore
{
    private const int CurrentCacheVersion = 3;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _cacheDirectory;
    private readonly string _cachePath;
    public int MissingImageCountOnLoad { get; private set; }

    public PresentationCacheStore(string? directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanvaStage", "cache");
        _cacheDirectory = directory;
        _cachePath = Path.Combine(directory, "presentation.json");
    }

    public CacheStatus GetStatus()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory)) return new CacheStatus(0, null, 0);
            FileInfo[] files = new DirectoryInfo(_cacheDirectory).EnumerateFiles("*", SearchOption.AllDirectories).ToArray();
            return new CacheStatus(files.Sum(file => file.Length), files.Length == 0 ? null : files.Max(file => (DateTimeOffset)file.LastWriteTimeUtc), files.Length);
        }
        catch (IOException)
        {
            return new CacheStatus(0, null, 0);
        }
        catch (UnauthorizedAccessException)
        {
            return new CacheStatus(0, null, 0);
        }
    }

    public int RemoveObsoleteGenerations(PresentationDocument activeDocument)
    {
        var activeDirectories = activeDocument.Pages
            .Select(page => page.FallbackImagePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetDirectoryName(Path.GetFullPath(path!)))
            .Where(path => path is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        int removed = 0;
        if (!Directory.Exists(_cacheDirectory)) return removed;
        foreach (string presentationDirectory in Directory.EnumerateDirectories(_cacheDirectory))
        {
            foreach (string generationDirectory in Directory.EnumerateDirectories(presentationDirectory))
            {
                if (activeDirectories.Contains(Path.GetFullPath(generationDirectory))) continue;
                Directory.Delete(generationDirectory, true);
                removed++;
            }
            if (!Directory.EnumerateFileSystemEntries(presentationDirectory).Any()) Directory.Delete(presentationDirectory);
        }
        return removed;
    }

    public void Clear()
    {
        if (Directory.Exists(_cacheDirectory)) Directory.Delete(_cacheDirectory, true);
    }

    public PresentationDocument? Load()
    {
        MissingImageCountOnLoad = 0;
        try
        {
            if (!File.Exists(_cachePath))
            {
                return null;
            }

            string json = File.ReadAllText(_cachePath);
            CachedPresentation? cached = JsonSerializer.Deserialize<CachedPresentation>(json, SerializerOptions);
            if (cached is not { Version: CurrentCacheVersion, Document.Pages.Count: > 0 }) return null;

            PresentationPage[] pages = cached.Document.Pages.Select(page =>
            {
                if (string.IsNullOrWhiteSpace(page.FallbackImagePath) || File.Exists(page.FallbackImagePath)) return page;
                MissingImageCountOnLoad++;
                return page with { FallbackImagePath = null };
            }).ToArray();
            if (MissingImageCountOnLoad > 0)
                PrivacySafeLog.Write($"Cached presentation is missing {MissingImageCountOnLoad} page image(s); retaining text and clearing broken image references.");
            return MissingImageCountOnLoad == 0 ? cached.Document : cached.Document with { Pages = pages };
        }
        catch (Exception exception)
        {
            PrivacySafeLog.WriteException("Presentation cache could not be loaded", exception);
            return null;
        }
    }

    public void Save(PresentationDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            string? directory = Path.GetDirectoryName(_cachePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = _cachePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new CachedPresentation(CurrentCacheVersion, document), SerializerOptions));
            File.Move(temporaryPath, _cachePath, true);
        }
        catch (Exception exception)
        {
            PrivacySafeLog.WriteException("Presentation cache could not be saved", exception);
        }
    }

    private sealed record CachedPresentation(int Version, PresentationDocument Document);
}

public sealed record CacheStatus(long SizeBytes, DateTimeOffset? LastUpdatedAt, int FileCount)
{
    public string SizeText => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024d:0.0} KB",
        < 1024L * 1024 * 1024 => $"{SizeBytes / 1024d / 1024d:0.0} MB",
        _ => $"{SizeBytes / 1024d / 1024d / 1024d:0.0} GB"
    };
}
