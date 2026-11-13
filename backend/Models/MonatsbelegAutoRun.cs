using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Persisted Auto-Monatsbeleg attempt for a Vienna calendar month (retry / FA failed-row source).
/// The TSE-signed receipt itself lives on <see cref="PaymentDetails"/> (DEP).
/// </summary>
[Table("monatsbeleg_auto_runs")]
public sealed class MonatsbelegAutoRun : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    [Column("cash_register_id")]
    public Guid CashRegisterId { get; set; }

    [ForeignKey(nameof(CashRegisterId))]
    public CashRegister? CashRegister { get; set; }

    [Column("year")]
    public int Year { get; set; }

    [Column("month")]
    public int Month { get; set; }

    /// <summary><see cref="MonatsbelegAutoRunStatuses"/>.</summary>
    [Required]
    [MaxLength(16)]
    [Column("status")]
    public string Status { get; set; } = MonatsbelegAutoRunStatuses.Pending;

    [Column("attempt_count")]
    public int AttemptCount { get; set; }

    [MaxLength(500)]
    [Column("last_error")]
    public string? LastError { get; set; }

    [Column("last_attempt_utc")]
    public DateTime? LastAttemptUtc { get; set; }

    [Column("next_retry_utc")]
    public DateTime? NextRetryUtc { get; set; }

    [Column("payment_id")]
    public Guid? PaymentId { get; set; }

    [MaxLength(64)]
    [Column("correlation_id")]
    public string CorrelationId { get; set; } = string.Empty;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class MonatsbelegAutoRunStatuses
{
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Exhausted = "Exhausted";
}
