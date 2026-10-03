using CanvaStage.Core.Utilities;
using System.Globalization;
using Xunit;

namespace CanvaStage.Tests;

public sealed class DiagnosticTimestampTests
{
    [Theory]
    [InlineData("2026-10-03T17:52:13-04:00", "2026-10-03T21:52:13.0000000Z")]
    [InlineData("2026-10-03T21:52:13+00:00", "2026-10-03T21:52:13.0000000Z")]
    [InlineData("2026-10-04T03:22:13+05:30", "2026-10-03T21:52:13.0000000Z")]
    public void AllOffsetsUseTheSameUtcFormat(string input, string expected)
        => Assert.Equal(expected, DiagnosticTimestamp.FormatUtc(DateTimeOffset.Parse(input, CultureInfo.InvariantCulture)));

    [Fact]
    public void MissingTimestampIsExplicit()
        => Assert.Equal("unknown", DiagnosticTimestamp.FormatUtc(null));
}
