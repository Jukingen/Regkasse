using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// GDPR-style customer data rights request for a mandant (expired-license data management).
/// Types: View (auto/instant), Export (auto/&lt;24h), Delete (manual confirm + 7-day wait; non-RKSV only).
/// </summary>
[Table("tenant_data_rights_requests")]
public class TenantDataRightsRequest : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    /// <summary><see cref="TenantDataRightsRequestTypes"/>.</summary>
    [Required]
    [MaxLength(16)]
    [Column("request_type")]
    public string RequestType { get; set; } = TenantDataRightsRequestTypes.View;

    /// <summary><see cref="TenantDataRightsRequestStatuses"/>.</summary>
    [Required]
    [MaxLength(24)]
    [Column("status")]
    public string Status { get; set; } = TenantDataRightsRequestStatuses.Pending;

    /// <summary><see cref="TenantDataRightsApprovalModes"/>.</summary>
    [Required]
    [MaxLength(16)]
    [Column("approval_mode")]
    public string ApprovalMode { get; set; } = TenantDataRightsApprovalModes.Auto;

    [MaxLength(450)]
    [Column("requested_by_user_id")]
    public string? RequestedByUserId { get; set; }

    [Column("requested_at_utc")]
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    [Column("reason")]
    public string? Reason { get; set; }

    [Column("approved_at_utc")]
    public DateTime? ApprovedAtUtc { get; set; }

    [Column("processing_deadline_utc")]
    public DateTime? ProcessingDeadlineUtc { get; set; }

    [Column("ready_at_utc")]
    public DateTime? ReadyAtUtc { get; set; }

    [Column("completed_at_utc")]
    public DateTime? CompletedAtUtc { get; set; }

    [MaxLength(450)]
    [Column("completed_by_user_id")]
    public string? CompletedByUserId { get; set; }

    /// <summary>Relative path under App_Data for export ZIP artifacts.</summary>
    [MaxLength(1024)]
    [Column("artifact_relative_path")]
    public string? ArtifactRelativePath { get; set; }

    [MaxLength(260)]
    [Column("artifact_file_name")]
    public string? ArtifactFileName { get; set; }

    [Column("artifact_byte_size")]
    public long? ArtifactByteSize { get; set; }

    /// <summary>Opaque download token for public link (expires via <see cref="DownloadExpiresAtUtc"/>).</summary>
    [MaxLength(64)]
    [Column("download_token")]
    public string? DownloadToken { get; set; }

    [Column("download_expires_at_utc")]
    public DateTime? DownloadExpiresAtUtc { get; set; }

    /// <summary>JSON payload for View results (inventory summary).</summary>
    [Column("view_payload_json")]
    public string? ViewPayloadJson { get; set; }

    /// <summary>Links Delete requests to <see cref="TenantDataDeletionRequest"/>.</summary>
    [Column("linked_deletion_request_id")]
    public Guid? LinkedDeletionRequestId { get; set; }

    [ForeignKey(nameof(LinkedDeletionRequestId))]
    public virtual TenantDataDeletionRequest? LinkedDeletionRequest { get; set; }

    [MaxLength(1000)]
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

public static class TenantDataRightsRequestTypes
{
    public const string View = "view";
    public const string Export = "export";
    public const string Delete = "delete";

    public static bool IsKnown(string? value) =>
        string.Equals(value, View, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Export, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Delete, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string value) => value.Trim().ToLowerInvariant();
}

public static class TenantDataRightsRequestStatuses
{
    public const string Pending = "pending";
    /// <summary>Delete requests awaiting Super Admin awareness / Manager confirmation.</summary>
    public const string PendingApproval = "pending_approval";
    public const string Approved = "approved";
    public const string Processing = "processing";
    public const string Ready = "ready";
    public const string Confirmed = "confirmed";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public static class TenantDataRightsApprovalModes
{
    public const string Auto = "auto";
    public const string Manual = "manual";
}
