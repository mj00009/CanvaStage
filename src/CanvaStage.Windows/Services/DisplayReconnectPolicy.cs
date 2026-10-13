namespace CanvaStage.Windows.Services;

/// <summary>Offers only a newly available, unambiguous remembered Extended display.</summary>
internal sealed class DisplayReconnectPolicy
{
    private bool _initialized;
    private bool _wasAvailable;
    private string? _previousId;

    public DisplayOption? Observe(IReadOnlyList<DisplayOption> displays, string? rememberedId)
    {
        DisplayOption? target = ResolveTarget(displays, rememberedId);
        bool offer = _initialized && string.Equals(_previousId, rememberedId, StringComparison.OrdinalIgnoreCase)
            && !_wasAvailable && target is not null;
        _initialized = true;
        _previousId = rememberedId;
        _wasAvailable = target is not null;
        return offer ? target : null;
    }

    public static DisplayOption? ResolveTarget(IReadOnlyList<DisplayOption> displays, string? rememberedId)
    {
        if (string.IsNullOrWhiteSpace(rememberedId)) return null;
        DisplayOption[] matches = displays.Where(display => display.MatchesId(rememberedId)).ToArray();
        return matches.Length == 1 && !matches[0].IsPrimary ? matches[0] : null;
    }
}
