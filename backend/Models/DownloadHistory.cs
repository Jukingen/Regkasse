using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Tenant-scoped record of an admin export / file download (metadata only; retention via DownloadHistory options).
/// </summary>
[Table("download_history")]
public class DownloadHistory : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>Identity user id (GUID string).</summary>
    [Required]
    [MaxLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Column("file_name", TypeName = "text")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>File extension / kind label (e.g. json, pdf, csv, zip, txt).</summary>
    [Required]
    [MaxLength(64)]
    [Column("file_type")]
    public string FileType { get; set; } = string.Empty;

    [Column("file_size")]
    public long? FileSize { get; set; }

    /// <summary>
    /// Optional download path or opaque reference (never store secrets / signed query tokens).
    /// </summary>
    [Column("download_url", TypeName = "text")]
    public string? DownloadUrl { get; set; }

    [Column("downloaded_at")]
    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;

    [Column("ip_address", TypeName = "text")]
    public string? IpAddress { get; set; }

    [Column("user_agent", TypeName = "text")]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Optional export domain for re-download (e.g. <c>dep-export</c>, <c>invoice</c>).
    /// </summary>
    [MaxLength(64)]
    [Column("source_kind")]
    public string? SourceKind { get; set; }

    /// <summary>Optional source artifact id (e.g. dep_export_history.id, invoice id).</summary>
    [Column("source_id")]
    public Guid? SourceId { get; set; }

    /// <summary>Optional client-observed download duration in milliseconds (for slow-export analytics).</summary>
    [Column("duration_ms")]
    public int? DurationMs { get; set; }
}
