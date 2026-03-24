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

    [Fact]
    public void CalendarHalfOpenInstantBounds_BothDates_MatchesAustriaInclusiveCalendarRangeUtc()
    {
        var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 5));
        var (a, b) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 5));
        Assert.Equal(a, lo);
        Assert.Equal(b, hi);
    }

    [Fact]
    public void CalendarHalfOpenInstantBounds_StartOnly_LowerBoundOnly()
    {
        var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(
            new DateTime(2026, 5, 10), null);
        Assert.NotNull(lo);
        Assert.Null(hi);
    }

    [Fact]
    public void CalendarHalfOpenInstantBounds_EndOnly_SingleCalendarDayHalfOpen()
    {
        var end = new DateTime(2026, 5, 10);
        var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(null, end);
        var (a, b) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(end, end);
        Assert.Equal(a, lo);
        Assert.Equal(b, hi);
    }

    [Fact]
    public void CalendarHalfOpenInstantBounds_Neither_Nulls()
    {
        var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(null, null);
        Assert.Null(lo);
        Assert.Null(hi);
    }

    [Fact]
    public void FormatViennaUtcInstantAsYyyyMmDd_UsesViennaLocalDate_NotUtcCalendarDay()
    {
        var utc = new DateTime(2026, 1, 15, 23, 0, 0, DateTimeKind.Utc);
        Assert.Equal("20260116", PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMmDd(utc));
    }

    [Fact]
    public void FormatViennaUtcInstantAsYyyyMm_And_Yyyy_UseViennaLocal()
    {
        var utc = new DateTime(2025, 12, 31, 23, 30, 0, DateTimeKind.Utc);
        Assert.Equal("202601", PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMm(utc));
        Assert.Equal("2026", PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyy(utc));
    }

    [Fact]
    public void Dst_SpringForward_2026_March29_LocalDayLength_Is23Hours()
    {
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 3, 29);
        var (fromUtc, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(day);
        var span = toExclusiveUtc - fromUtc;
        Assert.InRange(span.TotalHours, 22.9, 23.1);
    }

    [Fact]
    public void Dst_FallBack_2026_October25_LocalDayLength_Is25Hours()
    {
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 10, 25);
        var (fromUtc, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(day);
        var span = toExclusiveUtc - fromUtc;
        Assert.InRange(span.TotalHours, 24.9, 25.1);
    }

    [Fact]
    public void Dst_SpringDay_AustriaInclusiveCalendarRangeUtc_IsSingleHalfOpenDay()
    {
        var d = new DateTime(2026, 3, 29, 0, 0, 0, DateTimeKind.Unspecified);
        var (lo, hi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(d, d);
        Assert.True(hi > lo);
        Assert.InRange((hi - lo).TotalHours, 22.9, 23.1);
    }

    [Fact]
    public void Dst_ViennaCalendarAnchorToPersistUtc_March29_UnspecifiedMidnight_MatchesWall()
    {
        var unspecifiedMidnight = new DateTime(2026, 3, 29, 0, 0, 0, DateTimeKind.Unspecified);
        var anchor = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(unspecifiedMidnight);
        Assert.Equal(DateTimeKind.Utc, anchor.Kind);
        Assert.Equal(PostgreSqlUtcDateTime.ToUtcForNpgsql(unspecifiedMidnight), anchor);
    }
}

