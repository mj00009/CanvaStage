using System.Globalization;

namespace CanvaStage.Core.Utilities;

/// <summary>One unambiguous UTC format for diagnostic entry and embedded timestamps.</summary>
public static class DiagnosticTimestamp
{
    public static string FormatUtc(DateTimeOffset? timestamp)
        => timestamp?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? "unknown";
}
