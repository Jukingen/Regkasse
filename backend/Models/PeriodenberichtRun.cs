using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Persisted frozen Periodenbericht artefact based on operational aggregates.
/// This is a reproducible snapshot and is intentionally separate from FinanzOnline submission state.
/// </summary>
[Table("periodenbericht_runs")]
public sealed class PeriodenberichtRun
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string PeriodPreset { get; set; } = "custom";

    [Required]
    public DateTime PeriodStartLocalDate { get; set; }

    [Required]
    public DateTime PeriodEndLocalDate { get; set; }

    [Required]
    public DateTime PeriodStartUtc { get; set; }

    [Required]
    public DateTime PeriodEndUtc { get; set; }

    [Required]
    [MaxLength(20)]
    public string ScopeKind { get; set; } = "Company";

    public Guid? CashRegisterId { get; set; }

    [MaxLength(450)]
    public string? CashierId { get; set; }

    public int? PaymentMethodFilter { get; set; }
    public bool ActiveOnly { get; set; } = true;

    [Required]
    [Column(TypeName = "jsonb")]
    public string QueryParametersJson { get; set; } = "{}";

    [Required]
    [MaxLength(64)]
    public string QueryParametersHash { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "jsonb")]
    public string SnapshotJson { get; set; } = "{}";

    [Required]
    [MaxLength(64)]
    public string SnapshotHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string SnapshotSchemaVersion { get; set; } = "1.0";

    public int PaymentRowCount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrossTotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxTotalAmount { get; set; }

    public int RefundRowCount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RefundAmountTotal { get; set; }

    [Column(TypeName = "jsonb")]
    public string WarningsJson { get; set; } = "[]";

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ExportProfileKey { get; set; }

    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    [ForeignKey(nameof(CashRegisterId))]
    public CashRegister? CashRegister { get; set; }
}
