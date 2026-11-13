using KasseAPI_Final.Time;

namespace KasseAPI_Final.Rksv;

/// <summary>
/// Honest late (nachträglich / verspätet) metadata for RKSV Sonderbelege on <c>payment_details</c>.
/// Never backdates fiscal timestamps — only records which period was covered and whether creation was late.
/// </summary>
public static class RksvSpecialReceiptLateCreation
{
    /// <summary>Last Vienna calendar day covered by a Monatsbeleg (persisted as UTC anchor).</summary>
    public static DateTime MonatsbelegIntendedPeriodEndDate(int year, int month)
    {
        var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Unspecified);
        return PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(lastDay);
    }

    /// <summary>Last Vienna calendar day covered by a Jahresbeleg (December 31, UTC anchor).</summary>
    public static DateTime JahresbelegIntendedPeriodEndDate(int year)
    {
        var lastDay = new DateTime(year, 12, 31, 0, 0, 0, DateTimeKind.Unspecified);
        return PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(lastDay);
    }

    /// <summary>
    /// True when the Monatsbeleg is created for a past Vienna month (nachträglich) or past its legal deadline (verspätet).
    /// </summary>
    public static bool IsMonatsbelegLateCreated(int year, int month, DateTime? utcNow = null) =>
        MonatsbelegPastMonthPolicy.IsPastMonth(year, month, utcNow)
        || MonatsbelegPastMonthPolicy.ComputeDaysLate(year, month, utcNow) > 0;

    /// <summary>
    /// Whole days a Jahresbeleg is late after the legal deadline (January 31 of the following Vienna year).
    /// </summary>
    public static int ComputeJahresbelegDaysLate(int year, DateTime? utcNow = null)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        var deadline = new DateTime(year + 1, 1, 31);
        var days = (local.Date - deadline.Date).Days;
        return days < 0 ? 0 : days;
    }

    /// <summary>
    /// True when Jahresbeleg is created after January 31 of the following year, or for a prior Vienna year (nachträglich).
    /// </summary>
    public static bool IsJahresbelegLateCreated(int year, DateTime? utcNow = null)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, PostgreSqlUtcDateTime.AustriaTimeZone);
        if (year < local.Year)
            return true;
        return ComputeJahresbelegDaysLate(year, utcNow) > 0;
    }
}
