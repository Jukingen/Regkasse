using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Super Admin four-eyes approval for a critical tenant/admin action.
/// Identity user ids are strings (AspNetUsers); <see cref="RequestedBy"/> / <see cref="ApprovedBy"/> match that shape.
/// </summary>
[Table("approval_requests")]
public class ApprovalRequest
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Target tenant when the action is tenant-scoped; null for platform-wide actions.</summary>
    [Column("tenant_id")]
    public Guid? TenantId { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("requested_by")]
    public string RequestedBy { get; set; } = string.Empty;

    [MaxLength(450)]
    [Column("approved_by")]
    public string? ApprovedBy { get; set; }

    /// <summary><see cref="CriticalActionType"/> name (e.g. SchlussbelegCreation, TenantDeletion).</summary>
    [Required]
    [MaxLength(64)]
    [Column("action_type")]
    public string ActionType { get; set; } = string.Empty;

    /// <summary>Optional JSON snapshot of the intended request body / context.</summary>
    [Column("payload", TypeName = "text")]
    public string? Payload { get; set; }

    [Required]
    [MaxLength(16)]
    [Column("status")]
    public string Status { get; set; } = ApprovalRequestStatuses.Pending;

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    /// <summary>Default 24 hours from request.</summary>
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [MaxLength(1000)]
    [Column("reason")]
    public string? Reason { get; set; }

    [MaxLength(2000)]
    [Column("notes")]
    public string? Notes { get; set; }

    /// <summary>HTTP path hint used when issuing the critical-action approval token.</summary>
    [MaxLength(512)]
    [Column("path_hint")]
    public string? PathHint { get; set; }
}

public static class ApprovalRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Expired = "Expired";
    public const string Consumed = "Consumed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Pending, Approved, Rejected, Expired, Consumed,
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status.Trim());
}
