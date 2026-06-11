using System.Globalization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public static class DepExportScheduleTiming
{
    public static TimeSpan ParseTimeOfDay(string timeOfDay)
    {
        if (!TimeSpan.TryParseExact(
                timeOfDay.Trim(),
                ["hh\\:mm", "h\\:mm"],
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            throw new ArgumentException("timeOfDay must be HH:mm (UTC).", nameof(timeOfDay));
        }

        return parsed;
    }

    public static DateTime ComputeNextRunUtc(
        string scheduleType,
        int dayOfMonth,
        string timeOfDay,
        DateTime? fromUtc = null)
    {
        var normalized = DepExportScheduleTypes.Normalize(scheduleType);
        var from = fromUtc ?? DateTime.UtcNow;
        var time = ParseTimeOfDay(timeOfDay);
        var day = Math.Clamp(dayOfMonth, 1, 31);

        return normalized switch
        {
            DepExportScheduleTypes.Daily => NextDaily(from, time),
            DepExportScheduleTypes.Weekly => NextWeekly(from, time),
            DepExportScheduleTypes.Monthly => NextMonthly(from, day, time),
            DepExportScheduleTypes.Yearly => NextYearly(from, day, time),
            _ => throw new ArgumentOutOfRangeException(nameof(scheduleType)),
        };
    }

    public static (DateTime FromUtc, DateTime ToUtc) ResolveExportWindow(string scheduleType, DateTime runAtUtc)
    {
        var normalized = DepExportScheduleTypes.Normalize(scheduleType);

        return normalized switch
        {
            DepExportScheduleTypes.Daily => (
                runAtUtc.Date.AddDays(-1),
                runAtUtc.Date.AddTicks(-1)),
            DepExportScheduleTypes.Weekly => (
                runAtUtc.AddDays(-7),
                runAtUtc),
            DepExportScheduleTypes.Monthly => BuildPreviousCalendarMonthWindow(runAtUtc),
            DepExportScheduleTypes.Yearly => (
                new DateTime(runAtUtc.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(runAtUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1)),
            _ => throw new ArgumentOutOfRangeException(nameof(scheduleType)),
        };
    }

    public static IReadOnlyList<string> ParseRecipientEmails(string? recipientEmails) =>
        string.IsNullOrWhiteSpace(recipientEmails)
            ? Array.Empty<string>()
            : recipientEmails
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToList();

    private static DateTime NextDaily(DateTime fromUtc, TimeSpan timeOfDay)
    {
        var candidate = fromUtc.Date + timeOfDay;
        return candidate <= fromUtc ? candidate.AddDays(1) : candidate;
    }

    private static DateTime NextWeekly(DateTime fromUtc, TimeSpan timeOfDay)
    {
        var candidate = fromUtc.Date + timeOfDay;
        while (candidate <= fromUtc)
            candidate = candidate.AddDays(7);
        return candidate;
    }

    private static DateTime NextMonthly(DateTime fromUtc, int dayOfMonth, TimeSpan timeOfDay)
    {
        for (var monthOffset = 0; monthOffset < 24; monthOffset++)
        {
            var monthStart = new DateTime(fromUtc.Year, fromUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(monthOffset);
            var day = Math.Min(dayOfMonth, DateTime.DaysInMonth(monthStart.Year, monthStart.Month));
            var candidate = new DateTime(
                monthStart.Year,
                monthStart.Month,
                day,
                timeOfDay.Hours,
                timeOfDay.Minutes,
                0,
                DateTimeKind.Utc);
            if (candidate > fromUtc)
                return candidate;
        }

        throw new InvalidOperationException("Could not compute next monthly DEP export run.");
    }

    private static DateTime NextYearly(DateTime fromUtc, int dayOfMonth, TimeSpan timeOfDay)
    {
        for (var year = fromUtc.Year; year <= fromUtc.Year + 2; year++)
        {
            var day = Math.Min(dayOfMonth, DateTime.DaysInMonth(year, 1));
            var candidate = new DateTime(year, 1, day, timeOfDay.Hours, timeOfDay.Minutes, 0, DateTimeKind.Utc);
            if (candidate > fromUtc)
                return candidate;
        }

        throw new InvalidOperationException("Could not compute next yearly DEP export run.");
    }

    private static (DateTime FromUtc, DateTime ToUtc) BuildPreviousCalendarMonthWindow(DateTime runAtUtc)
    {
        var previousMonth = runAtUtc.AddMonths(-1);
        var start = new DateTime(previousMonth.Year, previousMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1).AddTicks(-1);
        return (start, end);
    }
}
