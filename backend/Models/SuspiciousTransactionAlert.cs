using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

public enum SuspiciousAlertType
{
    HighValue = 1,
    MultipleStornos = 2,
    MultipleRefunds = 3,
    UnusualTime = 4,
    SameCardMultiple = 5,
    RapidTransactions = 6,
}

public enum SuspiciousAlertSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

public enum SuspiciousAlertStatus
{
    Open = 1,
    Acknowledged = 2,
    Dismissed = 3,
}

[Table("suspicious_transaction_alerts")]
public class SuspiciousTransactionAlert : BaseEntity, ITenantEntity
{
    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [Column("alert_type")]
    public SuspiciousAlertType AlertType { get; set; }

    [Required]
    [Column("severity")]
    public SuspiciousAlertSeverity Severity { get; set; }

    [Required]
    [Column("status")]
    public SuspiciousAlertStatus Status { get; set; } = SuspiciousAlertStatus.Open;

    [Column("payment_id")]
    public Guid? PaymentId { get; set; }

    [Column("customer_id")]
    public Guid? CustomerId { get; set; }

    [MaxLength(450)]
    [Column("user_id")]
    public string? UserId { get; set; }

    [Required]
    [Column("message", TypeName = "text")]
    public string Message { get; set; } = string.Empty;

    [Column("suggested_action", TypeName = "text")]
    public string? SuggestedAction { get; set; }

    [Column("details_json", TypeName = "jsonb")]
    public string? DetailsJson { get; set; }

    [Required]
    [MaxLength(120)]
    [Column("dedup_key")]
    public string DedupKey { get; set; } = string.Empty;

    [Required]
    [Column("detected_at_utc")]
    public DateTime DetectedAtUtc { get; set; }
}
