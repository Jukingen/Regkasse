namespace KasseAPI_Final.DTOs;

/// <summary>Body for POST /api/admin/fiscal-export/generate (same semantics as GET query parameters).</summary>
public sealed class FiscalExportGenerateRequestDto
{
    public Guid CashRegisterId { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public bool IncludeCsv { get; set; }
    public string Format { get; set; } = "json";
    public string? ExportProfile { get; set; }
    public string Lang { get; set; } = "de";
}
