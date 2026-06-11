using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

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

    [Required]
    [MaxLength(32)]
    [Column("status")]
    public string Status { get; set; } = DepExportStatus.Completed.ToString();

    [Column("error_message", TypeName = "text")]
    public string? ErrorMessage { get; set; }

    [MaxLength(500)]
    [Column("storage_path")]
    public string? StoragePath { get; set; }

    [Column("schedule_id")]
    public Guid? ScheduleId { get; set; }

    [Column("include_special_receipts")]
    public bool IncludeSpecialReceipts { get; set; } = true;

    [Column("include_daily_closings")]
    public bool IncludeDailyClosings { get; set; } = true;
}
