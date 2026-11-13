using System.Globalization;
using System.Text;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public static class FiskalyStatisticsCsvBuilder
{
    public static string Build(FiskalyStatisticsDto stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("section,key,value");
        sb.AppendLine(Row("range", "fromUtc", stats.FromUtc.ToString("O")));
        sb.AppendLine(Row("range", "toUtc", stats.ToUtc.ToString("O")));
        if (stats.TenantId is { } tenantId)
            sb.AppendLine(Row("range", "tenantId", tenantId.ToString("D")));
        sb.AppendLine(Row("kpi", "totalOperations", stats.Kpis.TotalOperations.ToString(inv)));
        sb.AppendLine(Row("kpi", "successRatePercent", stats.Kpis.SuccessRatePercent.ToString("0.##", inv)));
        sb.AppendLine(Row("kpi", "mostUsedOperationType", stats.Kpis.MostUsedOperationType ?? ""));
        sb.AppendLine(Row("kpi", "averageProcessingTimeMs", stats.Kpis.AverageProcessingTimeMs?.ToString("0.#", inv) ?? ""));
        sb.AppendLine(Row("kpi", "totalErrors", stats.Kpis.TotalErrors.ToString(inv)));
        sb.AppendLine(Row("kpi", "successCount", stats.Kpis.SuccessCount.ToString(inv)));
        sb.AppendLine(Row("kpi", "failedCount", stats.Kpis.FailedCount.ToString(inv)));
        foreach (var row in stats.ByOperationType)
            sb.AppendLine(Row("operationType", row.Key, row.Count.ToString(inv)));
        foreach (var row in stats.ByStatus)
            sb.AppendLine(Row("status", row.Key, row.Count.ToString(inv)));
        foreach (var row in stats.Daily)
            sb.AppendLine(Row("daily", row.Date, $"{row.Total}|{row.Success}|{row.Failed}"));
        foreach (var row in stats.Monthly)
            sb.AppendLine(Row("monthly", row.YearMonth, $"{row.Total}|{row.Success}|{row.Failed}"));
        return sb.ToString();
    }

    private static string Row(string section, string key, string value) =>
        $"{Escape(section)},{Escape(key)},{Escape(value)}";

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
