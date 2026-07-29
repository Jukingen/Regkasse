using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Period-based DEP export obligation (yearly / quarterly / monthly).
/// Distinct from cron automation <see cref="DepExportSchedule"/>.
/// </summary>
[Table("dep_export_compliance_periods")]
public class DepExportCompliancePeriod : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>Yearly, Quarterly, or Monthly.</summary>
    [Required]
    [MaxLength(16)]
    [Column("period_type")]
    public string PeriodType { get; set; } = DepExportPeriodTypes.Yearly;

    [Required]
    [Column("period_start")]
    public DateTime PeriodStart { get; set; }

    [Required]
    [Column("period_end")]
    public DateTime PeriodEnd { get; set; }

    /// <summary>Pending, InProgress, Completed, or Overdue.</summary>
    [Required]
    [MaxLength(16)]
    [Column("status")]
    public string Status { get; set; } = DepExportPeriodStatuses.Pending;

    [Column("exported_at")]
    public DateTime? ExportedAt { get; set; }

    [MaxLength(450)]
    [Column("exported_by")]
    public string? ExportedBy { get; set; }

    [MaxLength(260)]
    [Column("file_name")]
    public string? FileName { get; set; }

    /// <summary>SHA-256 hex of the exported payload when completed.</summary>
    [MaxLength(64)]
    [Column("file_hash")]
    public string? FileHash { get; set; }

    [Column("history_id")]
    public Guid? HistoryId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

public static class DepExportPeriodTypes
{
    public const string Yearly = "Yearly";
    public const string Quarterly = "Quarterly";
    public const string Monthly = "Monthly";
}

public static class DepExportPeriodStatuses
{
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Overdue = "Overdue";
}
