using System;
using KasseAPI_Final.Time;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PostgreSqlUtcDateTimeTests
{
    [Fact]
    public void ToUtcForNpgsql_Utc_PassesThroughWithUtcKind()
    {
        var input = new DateTime(2026, 3, 24, 12, 0, 0, DateTimeKind.Utc);
        var actual = PostgreSqlUtcDateTime.ToUtcForNpgsql(input);
        Assert.Equal(DateTimeKind.Utc, actual.Kind);
        Assert.Equal(input, actual);
    }

    [Fact]
    public void ToUtcForNpgsql_Unspecified_InterpretsAsAustriaWallClockAndReturnsUtc()
    {
        // yyyy-MM-dd style binding: midnight on a Vienna calendar day (CET in late March 2026).
        var unspecifiedMidnight = new DateTime(2026, 3, 24, 0, 0, 0, DateTimeKind.Unspecified);
        var actual = PostgreSqlUtcDateTime.ToUtcForNpgsql(unspecifiedMidnight);
        Assert.Equal(DateTimeKind.Utc, actual.Kind);

        var tz = PostgreSqlUtcDateTime.AustriaTimeZone;
        var expected = TimeZoneInfo.ConvertTimeToUtc(unspecifiedMidnight, tz);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AustriaLocalCalendarDayToUtcRange_MatchesTimeZoneInfoConvert()
    {
        var localMidnight = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 3, 24);
        var (fromUtc, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(localMidnight);
        Assert.Equal(DateTimeKind.Utc, fromUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, toExclusiveUtc.Kind);

        var tz = PostgreSqlUtcDateTime.AustriaTimeZone;
        var expectedFrom = TimeZoneInfo.ConvertTimeToUtc(localMidnight, tz);
        var expectedTo = TimeZoneInfo.ConvertTimeToUtc(localMidnight.AddDays(1), tz);
        Assert.Equal(expectedFrom, fromUtc);
        Assert.Equal(expectedTo, toExclusiveUtc);
    }

    [Fact]
    public void AustriaInclusiveCalendarRangeUtc_SingleDay_HalfOpenEnd()
    {
        var (fromUtc, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(
            new DateTime(2026, 3, 24, 15, 30, 0, DateTimeKind.Unspecified),
            new DateTime(2026, 3, 24, 8, 0, 0, DateTimeKind.Utc));

        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 3, 24);
        var (a, b) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(day);
        Assert.Equal(a, fromUtc);
        Assert.Equal(b, toExclusiveUtc);
    }

    [Fact]
    public void ViennaCalendarMidnightContainingInstant_UsesViennaCalendarForUtcInput()
    {
        // 2026-03-24 22:30 UTC -> still 2026-03-24 in Vienna (CET +1 -> local 23:30)
        var utc = new DateTime(2026, 3, 24, 22, 30, 0, DateTimeKind.Utc);
        var mid = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(utc);
        Assert.Equal(2026, mid.Year);
        Assert.Equal(3, mid.Month);
        Assert.Equal(24, mid.Day);
    }

    [Fact]
    public void InstantToPersistUtc_Unspecified_SetsUtcKind()
    {
        var unspecified = new DateTime(2026, 3, 24, 12, 0, 0, DateTimeKind.Unspecified);
        var actual = PostgreSqlUtcDateTime.InstantToPersistUtc(unspecified);
        Assert.Equal(DateTimeKind.Utc, actual.Kind);
        Assert.Equal(unspecified.Ticks, actual.Ticks);
    }

    [Fact]
    public void InstantToPersistUtc_Local_ConvertsToUtc()
    {
        var local = new DateTime(2026, 3, 24, 12, 0, 0, DateTimeKind.Local);
        var actual = PostgreSqlUtcDateTime.InstantToPersistUtc(local);
        Assert.Equal(DateTimeKind.Utc, actual.Kind);
        Assert.Equal(local.ToUniversalTime(), actual);
    }

    [Fact]
    public void ViennaCalendarAnchorToPersistUtc_UnspecifiedMidnight_MatchesViennaWallToUtc()
    {
        var unspecifiedMidnight = new DateTime(2026, 3, 24, 0, 0, 0, DateTimeKind.Unspecified);
        var anchor = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(unspecifiedMidnight);
        Assert.Equal(DateTimeKind.Utc, anchor.Kind);
        var expected = PostgreSqlUtcDateTime.ToUtcForNpgsql(unspecifiedMidnight);
        Assert.Equal(expected, anchor);
    }
}
