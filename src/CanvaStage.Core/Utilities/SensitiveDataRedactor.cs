using System.Text.RegularExpressions;

namespace CanvaStage.Core.Utilities;

public static partial class SensitiveDataRedactor
{
    public static string Redact(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        string redacted = AuthorizationHeader().Replace(value, "Authorization: [REDACTED]");
        redacted = QueryValue().Replace(redacted, "$1[REDACTED]");
        return BearerToken().Replace(redacted, "Bearer [REDACTED]");
    }

    [GeneratedRegex(@"Authorization\s*:\s*[^\r\n]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeader();

    [GeneratedRegex(@"(?i)([?&](?:code|access_token|refresh_token|client_secret)=)[^&\s]+")]
    private static partial Regex QueryValue();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._~+\-/=]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerToken();
}
