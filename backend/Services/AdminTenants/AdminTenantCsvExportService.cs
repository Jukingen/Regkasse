using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using KasseAPI_Final.Services.AdminTenants;

namespace KasseAPI_Final.Services.AdminTenants;

/// <summary>Builds UTF-8 CSV for Super Admin tenant inventory export.</summary>
public interface IAdminTenantCsvExportService
{
    byte[] BuildCsv(IReadOnlyList<AdminTenantListItemDto> rows);
}

public sealed class AdminTenantCsvExportService : IAdminTenantCsvExportService
{
    private sealed class TenantCsvRow
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string LicenseType { get; set; } = string.Empty;
        public string LicenseExpiry { get; set; } = string.Empty;
        public int RegisterCount { get; set; }
        public int UserCount { get; set; }
        public string LastActivity { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
    }

    public byte[] BuildCsv(IReadOnlyList<AdminTenantListItemDto> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        using var ms = new MemoryStream();
        // UTF-8 BOM for Excel
        var preamble = Encoding.UTF8.GetPreamble();
        ms.Write(preamble, 0, preamble.Length);

        using (var writer = new StreamWriter(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
        }))
        {
            csv.WriteRecords(rows.Select(Map));
            writer.Flush();
        }

        return ms.ToArray();
    }

    private static TenantCsvRow Map(AdminTenantListItemDto t) => new()
    {
        Name = t.Name,
        Slug = t.Slug,
        Status = t.Status,
        LicenseType = t.LicenseType?.ToString() ?? string.Empty,
        LicenseExpiry = t.LicenseValidUntilUtc?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty,
        RegisterCount = t.RegisterCount,
        UserCount = t.UserCount,
        LastActivity = t.LastActivityAtUtc?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty,
        CreatedAt = t.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
    };
}
