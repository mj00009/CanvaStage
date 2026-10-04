namespace CanvaStage.Windows.Services;

public sealed record DisplayOption(string Id, string ScreenDeviceName, string FriendlyName, int Number, int Width, int Height, bool IsPrimary)
{
    public string Label => $"Display {Number} — {FriendlyName} — {Width} × {Height} ({(IsPrimary ? "Primary" : "Extended")})";
    public bool MatchesId(string? id) => string.Equals(Id, id, StringComparison.OrdinalIgnoreCase) || string.Equals(ScreenDeviceName, id, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Restores a chosen display without falling back to another monitor.</summary>
internal static class DisplaySelection
{
    public static DisplayOption? Resolve(IReadOnlyList<DisplayOption> displays, string? preferredId, string? previousDeviceName = null)
    {
        if (string.IsNullOrWhiteSpace(preferredId)) return displays.FirstOrDefault();
        DisplayOption[] matches = displays.Where(display => display.MatchesId(preferredId)).ToArray();
        if (matches.Length == 1) return matches[0];
        // Duplicate identities require the user's exact current device, not the
        // first item in Windows enumeration order. Ambiguous startup stays unset.
        return matches.SingleOrDefault(display => string.Equals(display.ScreenDeviceName, previousDeviceName, StringComparison.OrdinalIgnoreCase));
    }
}
