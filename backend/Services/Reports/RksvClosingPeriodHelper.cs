using KasseAPI_Final.Time;

namespace KasseAPI_Final.Services.Reports;

internal static class RksvClosingPeriodHelper
{
    public static (DateTime StartUtc, DateTime EndUtc) MonthUtcRange(int year, int month)
    {
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var lastDay = DateTime.DaysInMonth(year, month);
        var monthEnd = new DateTime(year, month, lastDay, 0, 0, 0, DateTimeKind.Unspecified);
        var (startUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(monthStart);
        var (_, endUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(monthEnd);
        return (startUtc, endUtc);
    }

    public static (DateTime StartUtc, DateTime EndUtc) YearUtcRange(int year)
    {
        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var yearEnd = new DateTime(year, 12, 31, 0, 0, 0, DateTimeKind.Unspecified);
        var (startUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(yearStart);
        var (_, endUtc) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(yearEnd);
        return (startUtc, endUtc);
    }
}
