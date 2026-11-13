namespace KasseAPI_Final.Services.DataExport;

/// <summary>
/// Canonical GDPR / expired-license data export document
/// (<c>regkasse.tenant-data-export.v2</c> — single JSON inside ZIP as <c>data-export.json</c>).
/// </summary>
public sealed class TenantDataExportDocument
{
    public const string FormatVersion = "regkasse.tenant-data-export.v2";
    public const string ZipEntryName = "data-export.json";
    public const string RksvRetentionNote = "RKSV data retained for 7 years (GDPR exception)";

    public required TenantDataExportTenantSection Tenant { get; init; }
    public required TenantDataExportDataSection Data { get; init; }
    public required TenantDataExportRksvSection Rksv { get; init; }
    public string Format { get; init; } = FormatVersion;
}

public sealed class TenantDataExportTenantSection
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required DateTime ExportedAt { get; init; }
}

public sealed class TenantDataExportDataSection
{
    public IReadOnlyList<object> Products { get; init; } = Array.Empty<object>();
    public IReadOnlyList<object> Categories { get; init; } = Array.Empty<object>();
    public IReadOnlyList<object> Customers { get; init; } = Array.Empty<object>();
    /// <summary>RKSV — signatures / chain values masked.</summary>
    public IReadOnlyList<object> Payments { get; init; } = Array.Empty<object>();
    /// <summary>RKSV — signatures / QR / JWS masked.</summary>
    public IReadOnlyList<object> Receipts { get; init; } = Array.Empty<object>();
    /// <summary>RKSV — signatures / JWS masked.</summary>
    public IReadOnlyList<object> Invoices { get; init; } = Array.Empty<object>();
    public IReadOnlyList<object> Orders { get; init; } = Array.Empty<object>();
    public IReadOnlyList<object> Vouchers { get; init; } = Array.Empty<object>();
    public object? Settings { get; init; }
}

public sealed class TenantDataExportRksvSection
{
    public required string Note { get; init; }
    public required DateTime RetentionUntil { get; init; }
}
