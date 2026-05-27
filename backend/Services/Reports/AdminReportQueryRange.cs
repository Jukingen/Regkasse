using KasseAPI_Final.Time;

namespace KasseAPI_Final.Services.Reports;

internal static class AdminReportQueryRange
{
    internal static (DateTime FromUtc, DateTime EndBoundUtc, bool EndExclusive, DateTime RepStart, DateTime RepEnd) Resolve(
        DateTime? startDate,
        DateTime? endDate)
    {
        var nowUtc = DateTime.UtcNow;
        if (!startDate.HasValue && !endDate.HasValue)
        {
            var fromUtc = nowUtc.AddDays(-30);
            return (fromUtc, nowUtc, false, fromUtc, nowUtc);
        }

        var s = startDate ?? endDate!.Value;
        var e = endDate ?? startDate!.Value;
        var reportStart = new DateTime(s.Year, s.Month, s.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var reportEnd = new DateTime(e.Year, e.Month, e.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var (fromUtcCal, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(s, e);
        return (fromUtcCal, toExclusiveUtc, true, reportStart, reportEnd);
    }
}
