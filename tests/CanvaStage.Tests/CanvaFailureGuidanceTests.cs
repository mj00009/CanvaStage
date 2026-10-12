using CanvaStage.Core.Utilities;
using Xunit;

namespace CanvaStage.Tests;

public sealed class CanvaFailureGuidanceTests
{
    [Theory]
    [InlineData(401, "Connect Canva")]
    [InlineData(403, "permissions")]
    [InlineData(404, "Refresh")]
    [InlineData(429, "Wait a minute")]
    [InlineData(500, "Wait a moment")]
    [InlineData(503, "Wait a moment")]
    [InlineData(400, "Test connection")]
    public void ExplainsCauseAndRecoveryUsingCurrentControls(int status, string recovery)
    {
        string message = CanvaFailureGuidance.ForHttpStatus(status);
        Assert.Contains(recovery, message);
        Assert.Contains("cached presentation remains available", message);
        Assert.DoesNotContain("Test setup", message);
    }
}
