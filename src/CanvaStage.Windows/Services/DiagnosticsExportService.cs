using CanvaStage.Core.Utilities;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace CanvaStage.Windows.Services;

public sealed class DiagnosticsExportService
{
    public string Export(DiagnosticsSnapshot snapshot)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string destination = Path.Combine(desktop, $"CanvaStage-Diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        using ZipArchive archive = ZipFile.Open(destination, ZipArchiveMode.Create);
        WriteEntry(archive, "summary.txt", CreateSummary(snapshot));
        if (File.Exists(PrivacySafeLog.FilePath))
        {
            string sanitizedLog = SensitiveDataRedactor.Redact(File.ReadAllText(PrivacySafeLog.FilePath));
            WriteEntry(archive, "canvastage.log", sanitizedLog);
        }
        return destination;
    }

    private static string CreateSummary(DiagnosticsSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine("CanvaStage diagnostic bundle (credentials and presentation content excluded)");
        builder.AppendLine($"Created (UTC): {DiagnosticTimestamp.FormatUtc(DateTimeOffset.UtcNow)}");
        builder.AppendLine($"Version: {Assembly.GetExecutingAssembly().GetName().Version}");
        builder.AppendLine($"Windows: {Environment.OSVersion}");
        builder.AppendLine($"Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        builder.AppendLine($"Presentation pages: {snapshot.PresentationPageCount}");
        builder.AppendLine($"Selected page: {snapshot.SelectedPage}");
        builder.AppendLine($"Output live: {snapshot.IsOutputLive}");
        builder.AppendLine($"Presenter health: {snapshot.PresenterHealth}");
        builder.AppendLine($"Last presenter detection (operator-local status text): {snapshot.LastPresenterDetection ?? "none"}");
        builder.AppendLine($"Cache: {snapshot.CacheSummary}");
        builder.AppendLine("Displays:");
        foreach (string display in snapshot.Displays) builder.AppendLine($"- {display}");
        return SensitiveDataRedactor.Redact(builder.ToString());
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using StreamWriter writer = new(entry.Open());
        writer.Write(content);
    }
}

public sealed record DiagnosticsSnapshot(
    int PresentationPageCount,
    int SelectedPage,
    bool IsOutputLive,
    string PresenterHealth,
    string? LastPresenterDetection,
    string CacheSummary,
    IReadOnlyList<string> Displays);
