using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models;

/// <summary>DEP history status for API responses (string enum JSON: "Completed", not 2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DepExportStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
}

/// <summary>Tenant-scoped audit row for each RKSV DEP §7 export (manual or scheduled).</summary>
[Table("dep_export_history")]
public class DepExportHistory : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [Column("cash_register_id")]
    public Guid CashRegisterId { get; set; }

    [Required]
    [Column("from_utc")]
    public DateTime FromUtc { get; set; }

    [Required]
    [Column("to_utc")]
    public DateTime ToUtc { get; set; }

    [Required]
    [Column("exported_at")]
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(450)]
    [Column("exported_by_user_id")]
    public string ExportedByUserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(260)]
    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [Column("file_size_bytes")]
    public long FileSizeBytes { get; set; }

    [Required]
    [Column("signature_count")]
    public int SignatureCount { get; set; }

    [Required]
    [Column("group_count")]
    public int GroupCount { get; set; }

    /// <summary>Pre-F5 (legacy JSON payload) compact JWS count in this export. 0 = Prüftool-compatible payloads.</summary>
    [Required]
    [Column("legacy_jws_count")]
    public int LegacyJwsCount { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("status")]
    public string Status { get; set; } = DepExportStatus.Completed.ToString();

    [Column("error_message", TypeName = "text")]
    public string? ErrorMessage { get; set; }

    [MaxLength(1024)]
    [Column("storage_path")]
    public string? StoragePath { get; set; }

    /// <summary>Opaque short-lived download token (hex). Null when not issued or revoked.</summary>
    [MaxLength(64)]
    [Column("download_token")]
    public string? DownloadToken { get; set; }

    /// <summary>UTC expiry for <see cref="DownloadToken"/>.</summary>
    [Column("download_token_expires_at_utc")]
    public DateTime? DownloadTokenExpiresAtUtc { get; set; }

    /// <summary>When the hot/working download copy is considered expired (default ExportedAt + 7 days).</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Last successful file download timestamp (UTC).</summary>
    [Column("downloaded_at")]
    public DateTime? DownloadedAt { get; set; }

    /// <summary>Successful download count (id or token path).</summary>
    [Required]
    [Column("download_count")]
    public int DownloadCount { get; set; }

    [Column("schedule_id")]
    public Guid? ScheduleId { get; set; }

    [Column("include_special_receipts")]
    public bool IncludeSpecialReceipts { get; set; } = true;

    [Column("include_daily_closings")]
    public bool IncludeDailyClosings { get; set; } = true;

    /// <summary>True when the export was created while RKSV demo / TSE simulation was active.</summary>
    [Column("is_simulated")]
    public bool IsSimulated { get; set; }

    /// <summary>Operator note when <see cref="IsSimulated"/>; null otherwise.</summary>
    [MaxLength(500)]
    [Column("simulation_note")]
    public string? SimulationNote { get; set; }

    /// <summary>Pending, Passed, Failed, or Skipped.</summary>
    [MaxLength(16)]
    [Column("validation_status")]
    public string? ValidationStatus { get; set; }

    [Column("validated_at")]
    public DateTime? ValidatedAt { get; set; }

    [Column("validation_report_json", TypeName = "jsonb")]
    public string? ValidationReportJson { get; set; }

    [Column("archived_at")]
    public DateTime? ArchivedAt { get; set; }

    [MaxLength(1024)]
    [Column("archive_path")]
    public string? ArchivePath { get; set; }

    [MaxLength(64)]
    [Column("archive_checksum")]
    public string? ArchiveChecksum { get; set; }

    [Column("retention_until")]
    public DateTime? RetentionUntil { get; set; }

    [Column("purged_at")]
    public DateTime? PurgedAt { get; set; }

    [MaxLength(200)]
    [Column("purge_reason")]
    public string? PurgeReason { get; set; }
}

public static class DepExportValidationStatuses
{
    public const string Pending = "Pending";
    public const string Passed = "Passed";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}
