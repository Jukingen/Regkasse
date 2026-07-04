using KasseAPI_Final.Rksv;
using KasseAPI_Final.Time;
using Xunit;

namespace KasseAPI_Final.Tests;

public class RksvSpecialReceiptLateCreationTests
{
    [Fact]
    public void MonatsbelegIntendedPeriodEndDate_UsesLastDayOfMonth()
    {
        var date = RksvSpecialReceiptLateCreation.MonatsbelegIntendedPeriodEndDate(2026, 2);
        var local = TimeZoneInfo.ConvertTimeFromUtc(date, PostgreSqlUtcDateTime.AustriaTimeZone);
        Assert.Equal(2026, local.Year);
        Assert.Equal(2, local.Month);
        Assert.Equal(28, local.Day);
    }

    [Fact]
    public void IsMonatsbelegLateCreated_PastMonth_ReturnsTrue_EvenWithinGrace()
    {
        var utcNow = new DateTime(2026, 2, 5, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(RksvSpecialReceiptLateCreation.IsMonatsbelegLateCreated(2026, 1, utcNow));
        Assert.Equal(0, MonatsbelegPastMonthPolicy.ComputeDaysLate(2026, 1, utcNow));
    }

    [Fact]
    public void ComputeJahresbelegDaysLate_AfterJanuary31_ReturnsPositive()
    {
        var utcNow = new DateTime(2027, 2, 10, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(10, RksvSpecialReceiptLateCreation.ComputeJahresbelegDaysLate(2026, utcNow));
        Assert.True(RksvSpecialReceiptLateCreation.IsJahresbelegLateCreated(2026, utcNow));
    }

    [Fact]
    public void IsJahresbelegLateCreated_PriorYear_ReturnsTrue()
    {
        var utcNow = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(RksvSpecialReceiptLateCreation.IsJahresbelegLateCreated(2025, utcNow));
    }
}
