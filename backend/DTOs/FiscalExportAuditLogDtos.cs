namespace KasseAPI_Final.DTOs;

/// <summary>Unified list/detail projection for DEP/fiscal-export download audit rows.</summary>
public class FiscalExportAuditLogListItemDto
{
    public Guid Id { get; set; }
    public DateTime DownloadTimeUtc { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string ExportTypeLabel { get; set; } = string.Empty;
    /// <summary>True when the export request asked for RKSV DEP CSV fragments (may accompany JSON envelope).</summary>
    public bool IncludesCsvFragment { get; set; }
    public DateTime? ExportPeriodFromUtc { get; set; }
    public DateTime? ExportPeriodToUtc { get; set; }
    /// <summary>Heuristic size estimate from receipt/closing counts; not measured from wire bytes.</summary>
    public long? EstimatedFileSizeBytes { get; set; }
    public bool Success { get; set; }
    /// <summary>True when exported period spans more than one calendar year (potential bulk extraction).</summary>
    public bool LongRangeBulkWarning { get; set; }
}

public sealed class FiscalExportAuditLogDetailDto : FiscalExportAuditLogListItemDto
{
    public string Action { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string UserRole { get; set; } = string.Empty;
    public string? RequestDataJson { get; set; }
    public string? ResponseDataJson { get; set; }
    public string? ErrorDetails { get; set; }
}

public sealed class FiscalExportAuditLogsPagedResponseDto
{
    public IReadOnlyList<FiscalExportAuditLogListItemDto> Items { get; set; } = Array.Empty<FiscalExportAuditLogListItemDto>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>Query filter: which primary transport the operator used (CSV fragment is tracked separately on each row).</summary>
public static class FiscalExportAuditExportTypeFilter
{
    public const string All = "all";
    public const string Pdf = "pdf";
    public const string Json = "json";
    public const string Csv = "csv";
}
