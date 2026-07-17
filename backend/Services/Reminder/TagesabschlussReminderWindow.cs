using KasseAPI_Final.Configuration;
using KasseAPI_Final.Time;

namespace KasseAPI_Final.Services.Reminder;

/// <summary>Pure helpers for Tagesabschluss evening reminder window (Europe/Vienna).</summary>
public static class TagesabschlussReminderWindow
{
    public static bool IsInsideReminderWindow(
        DateTime utcNow,
        TagesabschlussReminderOptions options)
    {
        var hour = Math.Clamp(options.ReminderHourVienna, 0, 23);
        var windowHours = Math.Clamp(options.WindowHours, 1, 12);

        var viennaLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            PostgreSqlUtcDateTime.AustriaTimeZone);

        var windowStart = new DateTime(
            viennaLocal.Year,
            viennaLocal.Month,
            viennaLocal.Day,
            hour,
            0,
            0,
            DateTimeKind.Unspecified);
        var windowEnd = windowStart.AddHours(windowHours);

        return viennaLocal >= windowStart && viennaLocal < windowEnd;
    }

    public static string BuildDedupKey(Guid cashRegisterId, DateTime viennaBusinessDay) =>
        $"tagesabschluss_pending_{cashRegisterId:D}_{viennaBusinessDay:yyyy-MM-dd}";
}
