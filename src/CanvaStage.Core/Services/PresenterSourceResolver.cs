using System.Text.RegularExpressions;

namespace CanvaStage.Core.Services;

public sealed record PresenterSource(string Key, string WindowTitle, string Application, string? DesignId, int CurrentPage, int TotalPages)
{
    public string Label => $"{Application} — {WindowTitle} ({TotalPages} pages)";
}

public enum PresenterSyncState { Waiting, Following, ChooseWindow, Mismatch, Paused, Manual, Recovering }

public sealed record PresenterSyncDecision(PresenterSyncState State, string Message, PresenterSource? Source = null);

/// <summary>Page count is a guard, never proof of presentation identity.</summary>
public static partial class PresenterSourceResolver
{
    public static PresenterSyncDecision Resolve(IReadOnlyList<PresenterSource> sources, string designId, string title, int pageCount, string? confirmedKey)
    {
        if (designId == "demo-sunday")
            return new(PresenterSyncState.Waiting, "Load a Canva presentation to enable Presenter sync. The demo uses manual controls.");

        if (confirmedKey is not null)
        {
            PresenterSource? selected = sources.FirstOrDefault(source => source.Key == confirmedKey);
            if (selected is null)
                return new(PresenterSyncState.Waiting, "The chosen Presenter window is unavailable. Waiting for it to return, or choose another window.");
            return Validate(selected, designId, pageCount);
        }

        PresenterSource[] matching = sources.Where(source => source.DesignId is not null
            ? string.Equals(source.DesignId, designId, StringComparison.Ordinal)
            : TitleMatches(source.WindowTitle, title)).ToArray();
        if (matching.Length == 1) return Validate(matching[0], designId, pageCount);
        if (matching.Length > 1)
            return new(PresenterSyncState.ChooseWindow, "More than one Presenter window matches. Choose the window to follow.");
        if (sources.Count == 0)
            return new(PresenterSyncState.Waiting, "Waiting for Canva Presenter View. Manual controls remain available.");
        if (sources.All(source => source.DesignId is not null && source.DesignId != designId))
            return new(PresenterSyncState.Mismatch, "The open Presenter windows belong to another Canva presentation. Open the applied presentation in Presenter View.");
        return new(PresenterSyncState.ChooseWindow, "Canva did not expose a matching presentation identity. Choose a window and confirm it is showing the applied presentation.");
    }

    private static PresenterSyncDecision Validate(PresenterSource source, string designId, int pageCount)
    {
        if (source.DesignId is not null && source.DesignId != designId)
            return new(PresenterSyncState.Mismatch, "This window belongs to another Canva presentation. Apply that presentation or choose a different window.");
        if (source.TotalPages != pageCount)
            return new(PresenterSyncState.Mismatch, $"Presenter has {source.TotalPages} visible pages; CanvaStage has {pageCount}. Refresh and apply the matching presentation.");
        if (source.CurrentPage < 1 || source.CurrentPage > pageCount)
            return new(PresenterSyncState.Mismatch, "The Presenter page is outside the applied presentation. Waiting for a valid page.");
        string evidence = source.DesignId is not null ? "design verified" : "window title or operator confirmation";
        return new(PresenterSyncState.Following, $"Following page {source.CurrentPage} of {pageCount} in {source.Application} ({evidence}).", source);
    }

    public static bool TitleMatches(string windowTitle, string presentationTitle)
    {
        string normalizedTitle = Normalize(presentationTitle);
        if (normalizedTitle.Length == 0) return false;
        string candidate = Normalize(windowTitle);
        // Remove only known window suffixes; substring matches can confuse similarly named decks.
        while (WindowSuffix().IsMatch(candidate)) candidate = WindowSuffix().Replace(candidate, "").Trim();
        return string.Equals(candidate, normalizedTitle, StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetDesignId(string? documentUrl)
    {
        if (!Uri.TryCreate(documentUrl, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !(uri.Host.Equals("canva.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".canva.com", StringComparison.OrdinalIgnoreCase))) return null;
        string[] parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[0] == "design" && DesignIdPattern().IsMatch(parts[1]) ? parts[1] : null;
    }

    private static string Normalize(string value) => Whitespace().Replace(value.Trim(), " ");
    [GeneratedRegex(@"\s+[-–—|]\s+(?:Canva|Presenter view|Presenter|Presentation|Google Chrome|Microsoft Edge|Mozilla Firefox|Brave|Opera)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WindowSuffix();
    [GeneratedRegex(@"^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex DesignIdPattern();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
