using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DepExportScheduleTimingTests
{
    [Theory]
    [InlineData("02:00", 2, 0)]
    [InlineData("9:30", 9, 30)]
    public void ParseTimeOfDay_ParsesValidValues(string input, int hour, int minute)
    {
        var parsed = DepExportScheduleTiming.ParseTimeOfDay(input);
        Assert.Equal(hour, parsed.Hours);
        Assert.Equal(minute, parsed.Minutes);
    }

    [Fact]
    public void ComputeNextRunUtc_Monthly_UsesDayOfMonth()
    {
        var from = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var next = DepExportScheduleTiming.ComputeNextRunUtc(DepExportScheduleTypes.Monthly, 1, "02:00", from);
        Assert.Equal(new DateTime(2026, 7, 1, 2, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void ResolveExportWindow_Monthly_CoversPreviousCalendarMonth()
    {
        var runAt = new DateTime(2026, 3, 1, 2, 0, 0, DateTimeKind.Utc);
        var (from, to) = DepExportScheduleTiming.ResolveExportWindow(DepExportScheduleTypes.Monthly, runAt);
        Assert.Equal(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(2026, to.Year);
        Assert.Equal(2, to.Month);
        Assert.Equal(28, to.Day);
        Assert.True(to.Hour >= 23 || to.Minute >= 59);
    }

    [Fact]
    public void ParseRecipientEmails_SplitsCommaSeparatedList()
    {
        var emails = DepExportScheduleTiming.ParseRecipientEmails("a@x.com, b@y.com");
        Assert.Equal(["a@x.com", "b@y.com"], emails);
    }
}
