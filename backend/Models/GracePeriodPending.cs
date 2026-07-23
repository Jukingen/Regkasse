using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Models;

/// <summary>
/// Deferred critical action with an undo window. For RKSV Schlussbeleg the fiscal
/// receipt is created only when status becomes <see cref="GracePeriodStatuses.Executed"/>.
/// </summary>
[Table("grace_period_pendings")]
public class GracePeriodPending : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary><see cref="GracePeriodActionKinds"/> value.</summary>
    [Required]
    [MaxLength(64)]
    [Column("action_kind")]
    public string ActionKind { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("entity_type")]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    [Column("payload", TypeName = "jsonb")]
    public string? Payload { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Undo window end; executor runs at/after this instant while still Pending.</summary>
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Required]
    [MaxLength(16)]
    [Column("status")]
    public string Status { get; set; } = GracePeriodStatuses.Pending;

    [MaxLength(450)]
    [Column("cancelled_by")]
    public string? CancelledBy { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("executed_at")]
    public DateTime? ExecutedAt { get; set; }

    [MaxLength(1000)]
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>Optional link to <see cref="OperationLog"/> for post-action undo kinds.</summary>
    [Column("operation_log_id")]
    public Guid? OperationLogId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }
}
