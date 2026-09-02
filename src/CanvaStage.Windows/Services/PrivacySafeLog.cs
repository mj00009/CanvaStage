using CanvaStage.Core.Utilities;
using System.IO;

namespace CanvaStage.Windows.Services;

public static class PrivacySafeLog
{
    private static readonly object SyncRoot = new();
    private static readonly string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanvaStage", "canvastage.log");

    public static void Write(string message)
    {
        try
        {
            string? directory = Path.GetDirectoryName(LogPath);
            if (directory is null)
            {
                return;
            }

            Directory.CreateDirectory(directory);
            string line = $"[{DateTimeOffset.Now:O}] {SensitiveDataRedactor.Redact(message)}{Environment.NewLine}";
            lock (SyncRoot)
            {
                File.AppendAllText(LogPath, line);
            }
        }
        catch (Exception)
        {
            System.Diagnostics.Debug.WriteLine("CanvaStage could not write a diagnostic log entry.");
        }
    }
}
