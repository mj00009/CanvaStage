using CanvaStage.Core.Models;
using System.IO;
using System.Text.Json;

namespace CanvaStage.Windows.Services;

public sealed class PresentationCacheStore
{
    private const int CurrentCacheVersion = 3;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _cachePath;

    public PresentationCacheStore(string? directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanvaStage", "cache");
        _cachePath = Path.Combine(directory, "presentation.json");
    }

    public PresentationDocument? Load()
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                return null;
            }

            string json = File.ReadAllText(_cachePath);
            CachedPresentation? cached = JsonSerializer.Deserialize<CachedPresentation>(json, SerializerOptions);
            return cached is { Version: CurrentCacheVersion, Document.Pages.Count: > 0 } ? cached.Document : null;
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
