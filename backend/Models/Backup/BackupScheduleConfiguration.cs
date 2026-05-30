using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Per-tenant automated backup schedule (UTC cron). Backup runs remain deployment-scoped;
/// the worker enqueues one shared run when any enabled tenant schedule is due.
/// </summary>
[Table("backup_schedule_configurations")]
public sealed class BackupScheduleConfiguration : BaseTenantEntity
{
    /// <summary>Default UTC cron — daily at 02:00 (CronFormat.Standard).</summary>
    public const string DefaultScheduleCron = "0 2 * * *";

    public const int DefaultRetentionDays = 30;

    [Column("enabled")]
    public bool Enabled { get; set; }

    [Required]
    [MaxLength(120)]
    [Column("schedule_cron")]
    public string ScheduleCron { get; set; } = DefaultScheduleCron;

    [Column("retention_days")]
    public int RetentionDays { get; set; } = DefaultRetentionDays;

    [Column("last_run_at")]
    public DateTime? LastRunAt { get; set; }

    [Column("next_run_at")]
    public DateTime? NextRunAt { get; set; }
}
