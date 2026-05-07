using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Singleton (Id=1): automated backup cron + retention knobs (admin UI).
/// </summary>
[Table("backup_settings")]
public sealed class BackupSettings
{
    public const int SingletonId = 1;

    /// <summary>Default UTC cron — daily at 02:00 (CronFormat.Standard).</summary>
    public const string DefaultScheduleCron = "0 2 * * *";

    public const int DefaultRetentionDays = 30;

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("id")]
    public int Id { get; set; } = SingletonId;

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

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
