using System.Globalization;
using System.Text;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.License;

/// <summary>UTF-8 CSV (with BOM) for Super Admin license audit export.</summary>
public static class LicenseAuditCsvBuilder
{
    public static byte[] Build(IReadOnlyList<LicenseAuditLogItemDto> rows)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("CreatedAtUtc,TenantId,TenantName,Action,FromStatus,ToStatus,PerformedBy,Reason");
        foreach (var row in rows)
        {
            sb.Append(Csv(row.CreatedAtUtc.ToUniversalTime().ToString("o", inv))).Append(',');
            sb.Append(Csv(row.TenantId?.ToString("D"))).Append(',');
            sb.Append(Csv(row.TenantName)).Append(',');
            sb.Append(Csv(row.Action)).Append(',');
            sb.Append(Csv(row.FromStatus)).Append(',');
            sb.Append(Csv(row.ToStatus)).Append(',');
            sb.Append(Csv(row.PerformedBy)).Append(',');
            sb.Append(Csv(row.Reason));
            sb.AppendLine();
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
            return value;
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
