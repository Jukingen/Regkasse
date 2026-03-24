using KasseAPI_Final.Time;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Pins behavioral contracts documented on <see cref="PostgreSqlUtcDateTime"/> (no production logic changes).
/// </summary>
public sealed class PostgreSqlUtcDateTimeContractTests
{
    [Fact]
    public void AustriaInclusiveCalendarRangeUtc_EndCalendarBeforeStart_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(
                new DateTime(2026, 6, 10),
                new DateTime(2026, 6, 1)));
    }

    [Fact]
    public void ToUtcForNpgsql_And_InstantToPersistUtc_AreNotInterchangeable_ForUnspecifiedMidnight()
    {
        var unspecified = new DateTime(2026, 3, 24, 0, 0, 0, DateTimeKind.Unspecified);
        var forQuery = PostgreSqlUtcDateTime.ToUtcForNpgsql(unspecified);
        var forInstant = PostgreSqlUtcDateTime.InstantToPersistUtc(unspecified);
        Assert.NotEqual(forQuery, forInstant);
        Assert.Equal(DateTimeKind.Utc, forQuery.Kind);
        Assert.Equal(DateTimeKind.Utc, forInstant.Kind);
    }

    [Fact]
    public void CalendarHalfOpenInstantBounds_StartOnly_LowerMatchesAustriaLocalDayStartUtc()
    {
        var start = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Unspecified);
        var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(start, null);
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 6, 10);
        var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(day);
        Assert.Equal(fromUtc, lo);
        Assert.Null(hi);
    }

    [Fact]
    public void CalendarHalfOpenInstantBounds_EndOnly_MatchesSingleDayInclusiveCalendarRange()
    {
        var end = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Unspecified);
        var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(null, end);
        var (a, b) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(end, end);
        Assert.Equal(a, lo);
        Assert.Equal(b, hi);
    }

    [Fact]
    public void FormatViennaUtcInstantAsYyyyMmDd_DoesNotThrowOnUnspecified_UsesInstantToPersistUtcContract()
    {
        var unspecified = new DateTime(2026, 3, 24, 12, 0, 0, DateTimeKind.Unspecified);
        var formatted = PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMmDd(unspecified);
        Assert.Equal(
            PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMmDd(
                PostgreSqlUtcDateTime.InstantToPersistUtc(unspecified)),
            formatted);
    }
}
