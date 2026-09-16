using KasseAPI_Final.Time;

namespace KasseAPI_Final.Services;

/// <summary>Vienna-local cutoff helpers for automatic Tagesabschluss (previous calendar day).</summary>
public static class AutoTagesabschlussCutoff
{
    public static bool IsPastCutoff(DateTime utcNow, int hourVienna, int minuteVienna)
    {
        var utc = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, PostgreSqlUtcDateTime.AustriaTimeZone);
        var nowMinutes = local.Hour * 60 + local.Minute;
        var cutoffMinutes = Math.Clamp(hourVienna, 0, 23) * 60 + Math.Clamp(minuteVienna, 0, 59);
        return nowMinutes >= cutoffMinutes;
    }

    /// <summary>Previous Europe/Vienna calendar day (Unspecified midnight) relative to <paramref name="utcNow"/>.</summary>
    public static DateTime GetYesterdayBusinessDay(DateTime utcNow)
    {
        var utc = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, PostgreSqlUtcDateTime.AustriaTimeZone);
        var today = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(
            local.Year,
            local.Month,
            local.Day);
        return today.AddDays(-1);
    }

    public static bool IsLastDayOfMonth(DateTime businessDayLocal) =>
        businessDayLocal.Day == DateTime.DaysInMonth(businessDayLocal.Year, businessDayLocal.Month);

    public static bool IsLastDayOfYear(DateTime businessDayLocal) =>
        businessDayLocal.Month == 12 && businessDayLocal.Day == 31;
}
