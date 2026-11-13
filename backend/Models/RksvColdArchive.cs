using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>One cold-archive ZIP produced by <c>RksvDataCleanupService</c>.</summary>
[Table("rksv_cold_archive_runs")]
public sealed class RksvColdArchiveRun
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC cutoff used for this run (typically now − retention years).</summary>
    [Column("cutoff_utc")]
    public DateTime CutoffUtc { get; set; }

    [Required]
    [MaxLength(1024)]
    [Column("archive_path")]
    public string ArchivePath { get; set; } = string.Empty;

    [MaxLength(64)]
    [Column("sha256")]
    public string? Sha256 { get; set; }

    [Column("payment_count")]
    public int PaymentCount { get; set; }

    [Required]
    [MaxLength(24)]
    [Column("status")]
    public string Status { get; set; } = RksvColdArchiveRunStatuses.Succeeded;

    [MaxLength(1000)]
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    public ICollection<RksvColdArchiveItem> Items { get; set; } = new List<RksvColdArchiveItem>();
}

public static class RksvColdArchiveRunStatuses
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}

/// <summary>Payment row included in a cold-archive ZIP (live DB row remains for chain integrity).</summary>
[Table("rksv_cold_archive_items")]
public sealed class RksvColdArchiveItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("archive_run_id")]
    public Guid ArchiveRunId { get; set; }

    [ForeignKey(nameof(ArchiveRunId))]
    public RksvColdArchiveRun? ArchiveRun { get; set; }

    [Column("payment_detail_id")]
    public Guid PaymentDetailId { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("cash_register_id")]
    public Guid CashRegisterId { get; set; }

    [Column("payment_created_at_utc")]
    public DateTime PaymentCreatedAtUtc { get; set; }

    [Column("archived_at_utc")]
    public DateTime ArchivedAtUtc { get; set; } = DateTime.UtcNow;
}
