namespace CanvaStage.Windows.Services;

public sealed record DisplayOption(string Id, string Name, bool IsPrimary)
{
    public string Label => IsPrimary ? $"{Name} (Primary)" : Name;
}

public sealed class DisplayService
{
    public IReadOnlyList<DisplayOption> GetDisplays()
    {
        return System.Windows.Forms.Screen.AllScreens
            .Select(screen => new DisplayOption(screen.DeviceName, screen.DeviceName, screen.Primary))
            .ToArray();
    }

    public System.Windows.Forms.Screen? FindDisplay(string? id)
    {
        return System.Windows.Forms.Screen.AllScreens.FirstOrDefault(screen => string.Equals(screen.DeviceName, id, StringComparison.OrdinalIgnoreCase));
    }
}
