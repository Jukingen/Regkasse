using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SessionPlatformLabelTests
{
    [Theory]
    [InlineData("pos", "Android", "POS (Android)")]
    [InlineData("POS", "iOS", "POS (iOS)")]
    [InlineData("pos", "Windows", "POS (Web)")]
    [InlineData("pos", null, "POS (Web)")]
    [InlineData("admin", "Windows", "Admin")]
    [InlineData("admin", null, "Admin")]
    [InlineData(null, null, "Unknown")]
    public void Resolve_MapsClientAppAndOs(string? clientApp, string? os, string expected)
    {
        Assert.Equal(expected, SessionPlatformLabel.Resolve(clientApp, os));
    }
}
