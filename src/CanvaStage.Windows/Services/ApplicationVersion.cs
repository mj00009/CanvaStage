using System.Reflection;

namespace CanvaStage.Windows.Services;

internal static class ApplicationVersion
{
    // InformationalVersion retains prerelease labels; exclude the appended commit hash.
    public static string Current { get; } = typeof(ApplicationVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(ApplicationVersion).Assembly.GetName().Version?.ToString(3) ?? "unknown";

    public static string Label => $"CanvaStage v{Current}";
}
