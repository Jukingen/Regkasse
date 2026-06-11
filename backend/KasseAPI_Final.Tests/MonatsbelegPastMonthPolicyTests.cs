using KasseAPI_Final.Rksv;
using Xunit;

namespace KasseAPI_Final.Tests;

public class MonatsbelegPastMonthPolicyTests
{
    [Theory]
    [InlineData(1, "info")]
    [InlineData(2, "warning")]
    [InlineData(6, "warning")]
    [InlineData(7, "error")]
    public void ResolveSeverity_MapsMonthDiff(int monthDiff, string expected)
    {
        Assert.Equal(expected, MonatsbelegPastMonthPolicy.ResolveSeverity(monthDiff));
    }

    [Fact]
    public void BuildWarningMessage_ForVormonat_MentionsZulassig()
    {
        var message = MonatsbelegPastMonthPolicy.BuildWarningMessage(1);
        Assert.Contains("Vormonat", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("zulässig", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildWarningMessage_ForLongGap_MentionsSteuerberater()
    {
        var message = MonatsbelegPastMonthPolicy.BuildWarningMessage(8);
        Assert.Contains("Steuerberater", message, StringComparison.OrdinalIgnoreCase);
    }
}
