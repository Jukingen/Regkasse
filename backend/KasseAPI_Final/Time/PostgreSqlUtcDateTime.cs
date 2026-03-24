using System;

namespace KasseAPI_Final.Time;

/// <summary>
/// PostgreSQL <c>timestamptz</c> (Npgsql 6+) requires <see cref="DateTime"/> parameters with <see cref="DateTimeKind.Utc"/>.
/// Centralizes UTC conversion and Europe/Vienna business-day boundaries for Austria POS semantics.
/// </summary>
public static class PostgreSqlUtcDateTime
{
    /// <summary>IANA id on Linux/macOS; Windows may use <c>W. Europe Standard Time</c>.</summary>
    public static TimeZoneInfo AustriaTimeZone { get; } = ResolveAustriaTimeZone();

    private static TimeZoneInfo ResolveAustriaTimeZone()
    {
        foreach (var id in new[] { "Europe/Vienna", "W. Europe Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        throw new InvalidOperationException(
            "Could not resolve Austria business time zone (tried Europe/Vienna and W. Europe Standard Time).");
    }

    /// <summary>
    /// Converts a value for use as an EF/Npgsql query parameter against <c>timestamptz</c> columns.
    /// <list type="bullet">
    /// <item><description><see cref="DateTimeKind.Utc"/> — returned as-is.</description></item>
    /// <item><description><see cref="DateTimeKind.Local"/> — <see cref="DateTime.ToUniversalTime"/>.</description></item>
    /// <item><description><see cref="DateTimeKind.Unspecified"/> — interpreted as wall-clock time in <see cref="AustriaTimeZone"/> (typical for <c>yyyy-MM-dd</c> query params).</description></item>
    /// </list>
    /// </summary>
    public static DateTime ToUtcForNpgsql(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), AustriaTimeZone)
        };
    }

    /// <summary>Calendar date at 00:00 as Unspecified; combine only with Vienna range helpers.</summary>
    public static DateTime ViennaCalendarDateMidnightUnspecified(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>Today's calendar date in Europe/Vienna at local midnight (Unspecified wall components).</summary>
    public static DateTime GetViennaTodayCalendarMidnightUnspecified()
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AustriaTimeZone);
        return ViennaCalendarDateMidnightUnspecified(local.Year, local.Month, local.Day);
    }

    /// <summary>Half-open UTC range matching one Austria local calendar day: [fromInclusiveUtc, toExclusiveUtc).</summary>
    public static (DateTime FromInclusiveUtc, DateTime ToExclusiveUtc) AustriaLocalCalendarDayToUtcRange(
        DateTime austriaLocalMidnightUnspecified)
    {
        var day = austriaLocalMidnightUnspecified.Date;
        var localStart = DateTime.SpecifyKind(day, DateTimeKind.Unspecified);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, AustriaTimeZone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(localStart.AddDays(1), AustriaTimeZone);
        return (fromUtc, toUtc);
    }

    /// <summary>
    /// Inclusive Austria calendar range from <paramref name="startCalendarAny"/>'s date through <paramref name="endCalendarAny"/>'s date, as a UTC half-open interval.
    /// </summary>
    public static (DateTime FromInclusiveUtc, DateTime ToExclusiveUtc) AustriaInclusiveCalendarRangeUtc(
        DateTime startCalendarAny,
        DateTime endCalendarAny)
    {
        var startDay = ViennaCalendarDateMidnightUnspecified(
            startCalendarAny.Year, startCalendarAny.Month, startCalendarAny.Day);
        var endDay = ViennaCalendarDateMidnightUnspecified(
            endCalendarAny.Year, endCalendarAny.Month, endCalendarAny.Day);
        if (endDay < startDay)
            throw new ArgumentOutOfRangeException(nameof(endCalendarAny),
                "End calendar date must be greater than or equal to start calendar date.");

        var (fromUtc, _) = AustriaLocalCalendarDayToUtcRange(startDay);
        var (_, toExclusiveUtc) = AustriaLocalCalendarDayToUtcRange(endDay);
        return (fromUtc, toExclusiveUtc);
    }

    /// <summary>
    /// Austria local calendar midnight for the Vienna-local calendar day that contains <paramref name="value"/> after normalizing to an instant.
    /// </summary>
    public static DateTime ViennaCalendarMidnightContainingInstant(DateTime value)
    {
        var utcInstant = ToUtcForNpgsql(value);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcInstant, AustriaTimeZone);
        return ViennaCalendarDateMidnightUnspecified(local.Year, local.Month, local.Day);
    }

    /// <summary>
    /// Normalizes an <strong>instant</strong> (payment time, audit <c>Timestamp</c>, <c>OccurredAt</c>, <c>CreatedAt</c> from <c>UtcNow</c>, etc.)
    /// to <see cref="DateTimeKind.Utc"/> for Npgsql <c>timestamptz</c> persistence.
    /// <list type="bullet">
    /// <item><description><see cref="DateTimeKind.Utc"/> — unchanged.</description></item>
    /// <item><description><see cref="DateTimeKind.Local"/> — <see cref="DateTime.ToUniversalTime"/>.</description></item>
    /// <item><description><see cref="DateTimeKind.Unspecified"/> — treated as a UTC clock reading (same instant ticks, kind set to UTC). Fiscal/server code must use <see cref="DateTime.UtcNow"/> or otherwise UTC semantics; ambiguous local wall times must be converted before calling this.</description></item>
    /// </list>
    /// </summary>
    public static DateTime InstantToPersistUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Maps an Austria business-calendar day anchor to the UTC instant for that calendar date at 00:00 in <see cref="AustriaTimeZone"/>.
    /// Used for <see cref="KasseAPI_Final.Models.DailyClosing.ClosingDate"/> and similar POS closing labels stored as <c>timestamptz</c>.
    /// </summary>
    public static DateTime ViennaCalendarAnchorToPersistUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => ViennaLocalMidnightUtcFromUtcInstant(value),
            DateTimeKind.Local => ViennaLocalMidnightUtcFromUtcInstant(value.ToUniversalTime()),
            _ => TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified),
                AustriaTimeZone)
        };
    }

    private static DateTime ViennaLocalMidnightUtcFromUtcInstant(DateTime utcInstant)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcInstant, AustriaTimeZone);
        var localMidnight = DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localMidnight, AustriaTimeZone);
    }
}
