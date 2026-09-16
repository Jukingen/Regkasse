using KasseAPI_Final.Services;
using KasseAPI_Final.Time;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AutoTagesabschlussCutoffTests
{
    [Fact]
    public void IsPastCutoff_Cest_IsFalseJustBeforeVienna0300()
    {
        // 2026-09-14 00:59 UTC = 02:59 Europe/Vienna (CEST, UTC+2)
        var utc = new DateTime(2026, 9, 14, 0, 59, 0, DateTimeKind.Utc);
        Assert.False(AutoTagesabschlussCutoff.IsPastCutoff(utc, hourVienna: 3, minuteVienna: 0));
    }

    [Fact]
    public void IsPastCutoff_Cest_IsTrueAtVienna0300()
    {
        // 2026-09-14 01:00 UTC = 03:00 Europe/Vienna (CEST)
        var utc = new DateTime(2026, 9, 14, 1, 0, 0, DateTimeKind.Utc);
        Assert.True(AutoTagesabschlussCutoff.IsPastCutoff(utc, hourVienna: 3, minuteVienna: 0));
    }

    [Fact]
    public void IsPastCutoff_Cet_UsesUtcPlusOne()
    {
        // 2027-01-15 01:59 UTC = 02:59 Europe/Vienna (CET, UTC+1)
        var before = new DateTime(2027, 1, 15, 1, 59, 0, DateTimeKind.Utc);
        Assert.False(AutoTagesabschlussCutoff.IsPastCutoff(before, 3, 0));

        var at = new DateTime(2027, 1, 15, 2, 0, 0, DateTimeKind.Utc);
        Assert.True(AutoTagesabschlussCutoff.IsPastCutoff(at, 3, 0));
    }

    [Fact]
    public void GetYesterdayBusinessDay_ReturnsPreviousViennaCalendarDay()
    {
        var utc = new DateTime(2026, 9, 14, 1, 30, 0, DateTimeKind.Utc);
        var yesterday = AutoTagesabschlussCutoff.GetYesterdayBusinessDay(utc);
        var expected = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 9, 13);
        Assert.Equal(expected, yesterday);
    }

    [Fact]
    public void IsLastDayOfMonth_And_Year()
    {
        var sep30 = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 9, 30);
        Assert.True(AutoTagesabschlussCutoff.IsLastDayOfMonth(sep30));
        Assert.False(AutoTagesabschlussCutoff.IsLastDayOfYear(sep30));

        var dec31 = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 12, 31);
        Assert.True(AutoTagesabschlussCutoff.IsLastDayOfMonth(dec31));
        Assert.True(AutoTagesabschlussCutoff.IsLastDayOfYear(dec31));

        var sep29 = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 9, 29);
        Assert.False(AutoTagesabschlussCutoff.IsLastDayOfMonth(sep29));
    }
}
