using System.Globalization;
using System.Text;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public static class FiskalyErrorCsvBuilder
{
    public static string Build(FiskalyErrorStatsDto stats, IReadOnlyList<FiskalyErrorListItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(items);
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("section,key,value");
        sb.AppendLine(Row("range", "fromUtc", stats.FromUtc.ToString("O")));
        sb.AppendLine(Row("range", "toUtc", stats.ToUtc.ToString("O")));
        if (stats.TenantId is { } tenantId)
            sb.AppendLine(Row("range", "tenantId", tenantId.ToString("D")));
        sb.AppendLine(Row("kpi", "totalErrors", stats.Kpis.TotalErrors.ToString(inv)));
        sb.AppendLine(Row("kpi", "errorRatePercent", stats.Kpis.ErrorRatePercent.ToString("0.##", inv)));
        sb.AppendLine(Row("kpi", "totalOperations", stats.Kpis.TotalOperations.ToString(inv)));
        sb.AppendLine(Row("kpi", "mostCommonErrorCode", stats.Kpis.MostCommonErrorCode ?? ""));
        sb.AppendLine(Row("kpi", "mostCommonErrorCount", stats.Kpis.MostCommonErrorCount.ToString(inv)));
        sb.AppendLine(Row(
            "kpi",
            "tenantWithMostErrors",
            stats.Kpis.TenantWithMostErrorsName ?? stats.Kpis.TenantWithMostErrorsId?.ToString("D") ?? ""));
        foreach (var row in stats.TopErrors)
            sb.AppendLine(Row("topError", row.Key, row.Count.ToString(inv)));
        foreach (var row in stats.ByOperationType)
            sb.AppendLine(Row("byOperationType", row.Key, row.Count.ToString(inv)));
        sb.AppendLine("createdAtUtc,tenantId,tenantName,operationType,errorCode,errorMessage,userId,userDisplayName,receiptNumber,reviewStatus");
        foreach (var item in items)
        {
            sb.AppendLine(string.Join(',',
                Escape(item.CreatedAtUtc.ToString("O")),
                Escape(item.TenantId.ToString("D")),
                Escape(item.TenantName ?? ""),
                Escape(item.OperationType),
                Escape(item.ErrorCode ?? ""),
                Escape(item.ErrorMessage ?? ""),
                Escape(item.UserId),
                Escape(item.UserDisplayName ?? ""),
                Escape(item.ReceiptNumber ?? ""),
                Escape(item.ReviewStatus)));
        }

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
