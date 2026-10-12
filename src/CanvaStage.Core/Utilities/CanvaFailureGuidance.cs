namespace CanvaStage.Core.Utilities;

/// <summary>Safe explanations without echoing response bodies or authorization data.</summary>
public static class CanvaFailureGuidance
{
    public static string ForHttpStatus(int status) => status switch
    {
        401 => "Canva did not accept the saved authorization. In Settings & Diagnostics → Canva connection, choose Connect Canva to authorize again. The cached presentation remains available.",
        403 => "Canva denied access. Confirm the connected account can open the presentation and the integration has design:meta:read and design:content:read permissions. Reconnect Canva in Settings & Diagnostics after correcting access. The cached presentation remains available.",
        404 => "The requested Canva item is unavailable (404). It may have been removed or its access changed. In Setup → Advanced, Refresh the list and confirm the connected account can open the intended deck, then retry. The cached presentation remains available.",
        429 => "Canva is limiting requests (429). Wait a minute before retrying; automatic content checks will retry with a delay. The cached presentation remains available.",
        >= 500 and <= 599 => $"Canva's service could not complete the request ({status}). Wait a moment and retry. The cached presentation remains available. If this repeats, export diagnostics from Settings & Diagnostics.",
        _ => $"Canva could not complete this request ({status}). Retry once, then use Settings & Diagnostics → Canva connection → Test connection. If it persists, export diagnostics; the cached presentation remains available."
    };
}
