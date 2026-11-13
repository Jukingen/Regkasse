using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class UserAgentParserTests
{
    [Theory]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Chrome",
        "Windows")]
    [InlineData(
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15",
        "Safari",
        "macOS")]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0",
        "Microsoft Edge",
        "Windows")]
    [InlineData(
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1",
        "Safari",
        "iOS")]
    public void Parse_detects_browser_and_os(string ua, string expectedBrowser, string expectedOs)
    {
        var parsed = UserAgentParser.Parse(ua);
        Assert.Equal(expectedBrowser, parsed.Browser);
        Assert.Equal(expectedOs, parsed.OS);
        Assert.Contains(expectedBrowser, parsed.DeviceName, StringComparison.Ordinal);
        Assert.Contains(expectedOs, parsed.DeviceName, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_returns_empty_for_null_or_blank()
    {
        Assert.Equal(default, UserAgentParser.Parse(null));
        Assert.Equal(default, UserAgentParser.Parse("   "));
    }
}
