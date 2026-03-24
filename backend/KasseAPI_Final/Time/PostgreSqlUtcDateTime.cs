using System;
using System.Globalization;

namespace KasseAPI_Final.Time;

/// <summary>
/// PostgreSQL <c>timestamptz</c> (Npgsql 6+) requires <see cref="DateTime"/> parameters with <see cref="DateTimeKind.Utc"/>.
/// Centralizes UTC conversion and Europe/Vienna business-day boundaries for Austria POS semantics.
/// </summary>
/// <remarks>
/// <b>Which helper when (instant vs calendar vs query binding):</b>
/// <list type="bullet">
/// <item><description><see cref="ToUtcForNpgsql"/> — <strong>Query/binding only:</strong> HTTP/query parameters meant as Austria <strong>wall-clock or calendar date</strong> (usually <see cref="DateTimeKind.Unspecified"/>). Converts to UTC for EF/Npgsql parameters. <strong>Do not</strong> use for values already read from <c>timestamptz</c> as UTC instants.</description></item>
/// <item><description><see cref="InstantToPersistUtc"/> — <strong>Instant</strong> for persistence: event times, <c>CreatedAt</c>, <c>UtcNow</c>. Local→UTC; Unspecified→same ticks with <see cref="DateTimeKind.Utc"/> (treat ticks as a UTC clock reading, not Vienna wall).</description></item>
/// <item><description><see cref="ViennaCalendarAnchorToPersistUtc"/> — <strong>Vienna business-day anchor</strong> (e.g. <c>DailyClosing.ClosingDate</c>): the UTC instant of 00:00 Europe/Vienna for that calendar date when input is Unspecified midnight; if input is UTC/Local, normalizes to Vienna local date first then that day’s midnight UTC.</description></item>
/// <item><description><see cref="AustriaInclusiveCalendarRangeUtc"/> / <see cref="CalendarHalfOpenInstantBounds"/> — <strong>Calendar-range filters</strong> on instants: inclusive Austria start/end <em>dates</em> → UTC half-open <c>[from, to)</c> for LINQ.</description></item>
/// <item><description><see cref="FormatViennaUtcInstantAsYyyyMmDd"/> / <see cref="FormatViennaUtcInstantAsYyyyMm"/> / <see cref="FormatViennaUtcInstantAsYyyy"/> — <strong>Display/reference labels</strong> from a stored instant: Vienna local date/month/year. Non-UTC <see cref="DateTime.Kind"/> is normalized via <see cref="InstantToPersistUtc"/> (see remarks on those methods; no fail-fast by design).</description></item>
/// </list>
/// </remarks>
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
    /// Converts a value for use as an EF/Npgsql query parameter against <c>timestamptz</c> columns (query / binding).
    /// </summary>
    /// <remarks>
    /// <b>Expected input:</b> API/query strings deserialized as <see cref="DateTimeKind.Unspecified"/> (date-only or Austria-local wall time).
    /// <list type="bullet">
    /// <item><description><see cref="DateTimeKind.Utc"/> — returned as-is (already a UTC instant).</description></item>
    /// <item><description><see cref="DateTimeKind.Local"/> — <see cref="DateTime.ToUniversalTime"/>.</description></item>
    /// <item><description><see cref="DateTimeKind.Unspecified"/> — interpreted as wall-clock in <see cref="AustriaTimeZone"/> (e.g. <c>yyyy-MM-dd</c> query params).</description></item>
    /// </list>
    /// <b>Misuse:</b> Do not pass a row value already materialized as a UTC instant from EF unless that instant was truly stored as UTC with those ticks.
    /// For the same Unspecified midnight, <see cref="ToUtcForNpgsql"/> and <see cref="InstantToPersistUtc"/> produce <strong>different</strong> UTC instants — pick the semantics that match the parameter meaning (wall vs instant ticks).
    /// </remarks>
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

    /// <summary>
    /// Half-open UTC range for one Austria local calendar day: <c>[fromInclusiveUtc, toExclusiveUtc)</c>.
    /// </summary>
    /// <param name="austriaLocalMidnightUnspecified">Date component is used; time should be 00:00 (see <see cref="ViennaCalendarDateMidnightUnspecified"/>).</param>
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
    /// Inclusive Austria <strong>calendar dates</strong> from <paramref name="startCalendarAny"/> through <paramref name="endCalendarAny"/>, mapped to a UTC <strong>half-open</strong> interval on instants.
    /// </summary>
    /// <remarks>
    /// Only <see cref="DateTime.Year"/>, <see cref="DateTime.Month"/>, and <see cref="DateTime.Day"/> are used; time-of-day is ignored.
    /// The interval is <c>[FromInclusiveUtc, ToExclusiveUtc)</c>: lower inclusive, upper <strong>exclusive</strong> (end day’s following midnight in UTC).
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">End calendar date is before start calendar date.</exception>
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
    /// Austria calendar-day half-open bounds <c>[LowerInclusiveUtc, UpperExclusiveUtc)</c> for filtering <strong>instants</strong> (e.g. <c>TransactionDate</c>, audit <c>Timestamp</c>).
    /// </summary>
    /// <remarks>
    /// Uses the same calendar-date components as <see cref="AustriaInclusiveCalendarRangeUtc"/>.
    /// <paramref name="startDate"/> / <paramref name="endDate"/> are typically query date-only (<see cref="DateTimeKind.Unspecified"/>).
    /// <list type="bullet">
    /// <item><description><strong>Both null</strong> — <c>(null, null)</c>: caller applies no date filter.</description></item>
    /// <item><description><strong>Start only</strong> — lower bound = start of that Vienna calendar day UTC; upper <c>null</c> (no exclusive end).</description></item>
    /// <item><description><strong>End only</strong> — single Vienna calendar day: same as <see cref="AustriaInclusiveCalendarRangeUtc"/>(end, end).</description></item>
    /// <item><description><strong>Both set</strong> — inclusive calendar range from start date through end date → half-open UTC interval.</description></item>
    /// </list>
    /// </remarks>
    /// <returns>
    /// <c>(null, null)</c> when both inputs are null;
    /// lower-only when only <paramref name="startDate"/> is set (no upper bound);
    /// both bounds when both set or when only <paramref name="endDate"/> is set (single calendar day).
    /// </returns>
    public static (DateTime? LowerInclusiveUtc, DateTime? UpperExclusiveUtc) CalendarHalfOpenInstantBounds(
        DateTime? startDate,
        DateTime? endDate)
    {
        if (!startDate.HasValue && !endDate.HasValue)
            return (null, null);

        if (startDate.HasValue && endDate.HasValue)
        {
            var (a, b) = AustriaInclusiveCalendarRangeUtc(startDate.Value, endDate.Value);
            return (a, b);
        }

        if (startDate.HasValue)
        {
            var day = ViennaCalendarDateMidnightUnspecified(
                startDate.Value.Year, startDate.Value.Month, startDate.Value.Day);
            var (fromUtc, _) = AustriaLocalCalendarDayToUtcRange(day);
            return (fromUtc, null);
        }

        var (f, t) = AustriaInclusiveCalendarRangeUtc(endDate!.Value, endDate.Value);
        return (f, t);
    }

    /// <summary>
    /// Vienna local calendar <c>yyyyMMdd</c> for display/reference ids — <strong>not</strong> <see cref="DateTime"/> UTC calendar components.
    /// </summary>
    /// <remarks>
    /// <b>Expected input:</b> Prefer <see cref="DateTimeKind.Utc"/> from EF for <c>timestamptz</c> columns.
    /// <b>Non-UTC <see cref="DateTime.Kind"/>:</b> Normalized with <see cref="InstantToPersistUtc"/> (same rules as persistence) then projected to Vienna.
    /// We do <strong>not</strong> fail-fast on non-UTC kinds: throwing would break InMemory/tests and duplicate semantics already used when saving instants.
    /// Callers must not pass Austria <strong>calendar</strong> midnight here expecting <see cref="ToUtcForNpgsql"/> wall semantics — use UTC instants or <see cref="ViennaCalendarAnchorToPersistUtc"/> first for anchors.
    /// </remarks>
    public static string FormatViennaUtcInstantAsYyyyMmDd(DateTime utcInstant)
    {
        var utc = InstantToPersistUtc(utcInstant);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, AustriaTimeZone);
        return local.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    }

    /// <inheritdoc cref="FormatViennaUtcInstantAsYyyyMmDd" path="/summary"/>
    /// <remarks>Same contract as <see cref="FormatViennaUtcInstantAsYyyyMmDd"/> (Vienna local <c>yyyyMM</c>).</remarks>
    public static string FormatViennaUtcInstantAsYyyyMm(DateTime utcInstant)
    {
        var utc = InstantToPersistUtc(utcInstant);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, AustriaTimeZone);
        return local.ToString("yyyyMM", CultureInfo.InvariantCulture);
    }

    /// <inheritdoc cref="FormatViennaUtcInstantAsYyyyMmDd" path="/summary"/>
    /// <remarks>Same contract as <see cref="FormatViennaUtcInstantAsYyyyMmDd"/> (Vienna local calendar year).</remarks>
    public static string FormatViennaUtcInstantAsYyyy(DateTime utcInstant)
    {
        var utc = InstantToPersistUtc(utcInstant);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, AustriaTimeZone);
        return local.Year.ToString("D4", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Austria local calendar midnight (Unspecified) for the Vienna calendar day that contains <paramref name="value"/>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="ToUtcForNpgsql"/> to obtain a UTC instant, then Vienna local date — suitable for “which business day?” from mixed query inputs.
    /// </remarks>
    public static DateTime ViennaCalendarMidnightContainingInstant(DateTime value)
    {
        var utcInstant = ToUtcForNpgsql(value);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcInstant, AustriaTimeZone);
        return ViennaCalendarDateMidnightUnspecified(local.Year, local.Month, local.Day);
    }

    /// <summary>
    /// Normalizes an <strong>instant</strong> (payment time, audit <c>Timestamp</c>, <c>OccurredAt</c>, <c>CreatedAt</c> from <c>UtcNow</c>, etc.)
    /// to <see cref="DateTimeKind.Utc"/> for Npgsql <c>timestamptz</c> persistence.
    /// </summary>
    /// <remarks>
    /// <b>Not</b> for Austria date-only query parameters — use <see cref="ToUtcForNpgsql"/> for those.
    /// <list type="bullet">
    /// <item><description><see cref="DateTimeKind.Utc"/> — unchanged.</description></item>
    /// <item><description><see cref="DateTimeKind.Local"/> — <see cref="DateTime.ToUniversalTime"/>.</description></item>
    /// <item><description><see cref="DateTimeKind.Unspecified"/> — same ticks, <see cref="DateTimeKind.Utc"/> (UTC clock reading). Server code should prefer <see cref="DateTime.UtcNow"/> so Kind is already UTC.</description></item>
    /// </list>
    /// </remarks>
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
    /// Maps an Austria <strong>business-calendar day</strong> to the UTC instant for that date at 00:00 in <see cref="AustriaTimeZone"/> (stored <c>timestamptz</c> anchor).
    /// </summary>
    /// <remarks>
    /// <b><see cref="DateTimeKind.Unspecified"/>:</b> <see cref="DateTime.Date"/> as Vienna wall midnight → UTC (typical when binding <c>ClosingDate</c> from date pickers).
    /// <b><see cref="DateTimeKind.Utc"/> / <see cref="DateTimeKind.Local"/>:</b> first map to Vienna local calendar day containing that instant, then that day’s 00:00 Vienna as UTC (re-anchors “floating” instants to a business day).
    /// Do not confuse with <see cref="InstantToPersistUtc"/> (pure instant) or <see cref="ToUtcForNpgsql"/> (query param wall-clock) — see class remarks.
    /// </remarks>
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
