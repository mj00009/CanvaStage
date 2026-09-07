using CanvaStage.Core.Models;
using System.IO;
using System.Text.Json;

namespace CanvaStage.Windows.Services;

public sealed class JsonPreferencesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public JsonPreferencesStore(string? directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanvaStage");
        _settingsPath = Path.Combine(directory, "preferences.json");
    }

    public AppPreferences Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return AppPreferences.Default;
            }

            string json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppPreferences>(json, SerializerOptions) ?? AppPreferences.Default;
        }
        catch (Exception)
        {
            return AppPreferences.Default;
        }
    }

    public void Save(AppPreferences preferences)
    {
        string? directory = Path.GetDirectoryName(_settingsPath);
        if (directory is null)
        {
            return;
        }

        Directory.CreateDirectory(directory);
        string temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(preferences, SerializerOptions));
        File.Move(temporaryPath, _settingsPath, true);
    }
}
