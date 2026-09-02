using CanvaStage.Core.Utilities;
using Xunit;

namespace CanvaStage.Tests;

public sealed class SensitiveDataRedactorTests
{
    [Fact]
    public void RedactsAuthorizationHeadersAndOAuthQueryValues()
    {
        const string input = "Authorization: Bearer access-secret\nhttp://127.0.0.1/callback/?code=oauth-code&state=known-state&refresh_token=refresh-secret";

        string redacted = SensitiveDataRedactor.Redact(input);

        Assert.DoesNotContain("access-secret", redacted);
        Assert.DoesNotContain("oauth-code", redacted);
        Assert.DoesNotContain("refresh-secret", redacted);
        Assert.Contains("[REDACTED]", redacted);
    }

    [Fact]
    public void LeavesOrdinaryOperationalMessagesReadable()
    {
        const string input = "Canva export failed with status 403.";

        Assert.Equal(input, SensitiveDataRedactor.Redact(input));
    }
}
