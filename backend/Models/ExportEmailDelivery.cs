using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>How an export was delivered by email.</summary>
public static class ExportEmailDeliveryModes
{
    public const string Attachment = "attachment";
    public const string Link = "link";
}

/// <summary>Lifecycle status for an export email delivery or schedule row.</summary>
public static class ExportEmailDeliveryStatuses
{
    public const string Pending = "pending";
    public const string Scheduled = "scheduled";
    public const string Sent = "sent";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

/// <summary>Tenant-scoped history of export emails (attachment or download-link).</summary>
[Table("export_email_deliveries")]
public class ExportEmailDelivery : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(320)]
    [Column("recipient_email")]
    public string RecipientEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    [Column("message", TypeName = "text")]
    public string? Message { get; set; }

    [Required]
    [Column("file_name", TypeName = "text")]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("content_type")]
    public string ContentType { get; set; } = "application/octet-stream";

    [Column("file_size_bytes")]
    public long FileSizeBytes { get; set; }

    /// <summary><see cref="ExportEmailDeliveryModes"/>.</summary>
    [Required]
    [MaxLength(32)]
    [Column("delivery_mode")]
    public string DeliveryMode { get; set; } = ExportEmailDeliveryModes.Attachment;

    /// <summary><see cref="ExportEmailDeliveryStatuses"/>.</summary>
    [Required]
    [MaxLength(32)]
    [Column("status")]
    public string Status { get; set; } = ExportEmailDeliveryStatuses.Pending;

    [MaxLength(64)]
    [Column("source_kind")]
    public string? SourceKind { get; set; }

    [Column("source_id")]
    public Guid? SourceId { get; set; }

    /// <summary>SHA-256 hex of the opaque download token (link mode).</summary>
    [MaxLength(64)]
    [Column("download_token_hash")]
    public string? DownloadTokenHash { get; set; }

    [Column("download_expires_at_utc")]
    public DateTime? DownloadExpiresAtUtc { get; set; }

    /// <summary>Relative path under export-email storage root (never expose raw).</summary>
    [Column("artifact_relative_path", TypeName = "text")]
    public string? ArtifactRelativePath { get; set; }

    [Column("scheduled_for_utc")]
    public DateTime? ScheduledForUtc { get; set; }

    [Column("sent_at_utc")]
    public DateTime? SentAtUtc { get; set; }

    [Column("error_message", TypeName = "text")]
    public string? ErrorMessage { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
