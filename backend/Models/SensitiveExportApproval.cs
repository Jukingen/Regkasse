using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Super Admin approval gate for sensitive export downloads.</summary>
[Table("sensitive_export_approvals")]
public class SensitiveExportApproval
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid? TenantId { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("export_kind")]
    public string ExportKind { get; set; } = string.Empty;

    [Required]
    [MaxLength(450)]
    [Column("requester_user_id")]
    public string RequesterUserId { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("reason")]
    public string? Reason { get; set; }

    /// <summary>Optional resource id (backup run, rights request, audit job).</summary>
    [Column("resource_id")]
    public string? ResourceId { get; set; }

    [Required]
    [MaxLength(16)]
    [Column("status")]
    public string Status { get; set; } = SensitiveExportApprovalStatuses.Pending;

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    [Column("resolved_by_user_id")]
    public string? ResolvedByUserId { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(500)]
    [Column("resolution_note")]
    public string? ResolutionNote { get; set; }

    /// <summary>When approved, downloads are allowed until this UTC timestamp.</summary>
    [Column("valid_until")]
    public DateTime? ValidUntil { get; set; }
}

public static class SensitiveExportApprovalStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    public const string Consumed = "Consumed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Pending, Approved, Rejected, Cancelled, Consumed,
    };
}

/// <summary>Opaque time-limited download ticket (single-use preferred).</summary>
[Table("download_security_tickets")]
public class DownloadSecurityTicket
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(64)]
    [Column("token_hash")]
    public string TokenHash { get; set; } = string.Empty;

    [Column("tenant_id")]
    public Guid? TenantId { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("export_kind")]
    public string ExportKind { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("resource_id")]
    public string? ResourceId { get; set; }

    [Column("approval_id")]
    public Guid? ApprovalId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }
}
