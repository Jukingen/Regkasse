namespace KasseAPI_Final.DTOs;

public sealed class DepExportAuditEntryDto
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string ExportName { get; set; } = string.Empty;

    public Guid? ExportHistoryId { get; set; }

    public string? UserEmail { get; set; }

    public string? UserId { get; set; }

    public string? UserRole { get; set; }

    public DateTime ActionAt { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? Details { get; set; }
}

public sealed class DepExportAuditReportDto
{
    public Guid TenantId { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public DateTime FromUtc { get; set; }

    public DateTime ToUtc { get; set; }

    public int TotalEntries { get; set; }

    public Dictionary<string, int> CountsByAction { get; set; } = new();

    public DateTime? LastActionAt { get; set; }

    public string? LastAction { get; set; }

    public string? LastExportName { get; set; }

    public IReadOnlyList<DepExportAuditEntryDto> RecentEntries { get; set; } =
        Array.Empty<DepExportAuditEntryDto>();

    public string Disclaimer { get; set; } =
        "Operational DEP export audit trail — not an official BMF/RKSV certification artifact.";
}

public sealed class DepExportAuditTrailQuery
{
    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public string? Action { get; set; }

    public string? UserSearch { get; set; }

    public int Limit { get; set; } = 100;
}
