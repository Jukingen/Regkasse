namespace KasseAPI_Final.Models.Reports;

public sealed class FreezePeriodenberichtRequest
{
    public string PeriodPreset { get; set; } = "custom";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? CashRegisterId { get; set; }
    public string? CashierId { get; set; }
    public int? PaymentMethod { get; set; }
    public bool ActiveOnly { get; set; } = true;
    public string? ExportProfileKey { get; set; }
    public string? CorrelationId { get; set; }
    public string? Note { get; set; }
}

public sealed class PeriodenberichtRunListItemDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public string PeriodPreset { get; set; } = string.Empty;
    public DateTime PeriodStartLocalDate { get; set; }
    public DateTime PeriodEndLocalDate { get; set; }
    public string ScopeKind { get; set; } = string.Empty;
    public Guid? CashRegisterId { get; set; }
    public string? CashierId { get; set; }
    public int? PaymentMethodFilter { get; set; }
    public bool ActiveOnly { get; set; }
    public string SnapshotSchemaVersion { get; set; } = string.Empty;
    public int PaymentRowCount { get; set; }
    public decimal GrossTotalAmount { get; set; }
    public decimal TaxTotalAmount { get; set; }
    public decimal RefundAmountTotal { get; set; }
    public string QueryParametersHash { get; set; } = string.Empty;
    public string SnapshotHash { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? ExportProfileKey { get; set; }
}

public sealed class PeriodenberichtRunDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public string PeriodPreset { get; set; } = string.Empty;
    public DateTime PeriodStartLocalDate { get; set; }
    public DateTime PeriodEndLocalDate { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public string ScopeKind { get; set; } = string.Empty;
    public Guid? CashRegisterId { get; set; }
    public string? CashierId { get; set; }
    public int? PaymentMethodFilter { get; set; }
    public bool ActiveOnly { get; set; }
    public string QueryParametersHash { get; set; } = string.Empty;
    public string SnapshotHash { get; set; } = string.Empty;
    public string SnapshotSchemaVersion { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? ExportProfileKey { get; set; }
    public string? CorrelationId { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    public OperationalSummaryDto Summary { get; set; } = new();
}
