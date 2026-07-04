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

    [Fact]
    public void ComputeDaysLate_WithinGraceWindow_ReturnsZero()
    {
        // January 2026 deadline = 2026-01-31 + 7 = 2026-02-07; creating on 2026-02-05 is still on time.
        var utcNow = new DateTime(2026, 2, 5, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, MonatsbelegPastMonthPolicy.ComputeDaysLate(2026, 1, utcNow));
        Assert.False(MonatsbelegPastMonthPolicy.IsLateCreation(2026, 1, utcNow));
    }

    [Fact]
    public void ComputeDaysLate_PastDeadline_ReturnsPositiveDayCount()
    {
        // January 2026 deadline = 2026-02-07; creating on 2026-02-20 is 13 days late.
        var utcNow = new DateTime(2026, 2, 20, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(13, MonatsbelegPastMonthPolicy.ComputeDaysLate(2026, 1, utcNow));
        Assert.True(MonatsbelegPastMonthPolicy.IsLateCreation(2026, 1, utcNow));
    }

    [Fact]
    public void ComputeDaysLate_SameMonth_ReturnsZero()
    {
        // Creating the January Monatsbeleg during January itself is never late.
        var utcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, MonatsbelegPastMonthPolicy.ComputeDaysLate(2026, 1, utcNow));
        Assert.False(MonatsbelegPastMonthPolicy.IsLateCreation(2026, 1, utcNow));
    }
}
