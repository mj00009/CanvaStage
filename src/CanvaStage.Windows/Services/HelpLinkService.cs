using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Text.RegularExpressions;

namespace CanvaStage.Windows.Services;

/// <summary>Opens only our public documentation; never sends app state or credentials.</summary>
public sealed partial class HelpLinkService
{
    private const string BaseUrl = "https://mj00009.github.io/CanvaStage/";
    private readonly Action<Uri> _launch;

    public HelpLinkService(string version, Action<Uri>? launch = null)
    {
        // Stable releases have archived guides. Development/prerelease builds use the latest guide.
        Url = new Uri(BaseUrl + (StableVersion().IsMatch(version) ? $"v{version}/" : string.Empty));
        _launch = launch ?? (uri => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }));
    }

    public Uri Url { get; }

    public string? TryOpen()
    {
        try
        {
            _launch(Url);
            return null;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or SecurityException or NotSupportedException)
        {
            // Do not include browser exception text, which can contain local system details.
            return $"Could not open Help. Copy this address into a browser: {Url.AbsoluteUri} (internet access required).";
        }
    }

    [GeneratedRegex(@"\A(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex StableVersion();
}
