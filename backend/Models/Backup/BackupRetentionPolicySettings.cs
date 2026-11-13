using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Singleton (Id=1): Super Admin operational + legal backup retention knobs.
/// Cloud credentials are not stored here — see <c>Backup:CloudStorage</c>.
/// </summary>
[Table("backup_retention_policy_settings")]
public sealed class BackupRetentionPolicySettings
{
    public const int SingletonId = 1;
    public const int DefaultHotRetentionDays = BackupRetentionWindows.DefaultHotDays;
    public const int DefaultWarmRetentionDays = BackupRetentionWindows.DefaultWarmDays;
    public const int DefaultColdRetentionYears = 7;
    public const int MinColdRetentionYears = 7;
    public const int MaxColdRetentionYears = 10;

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("id")]
    public int Id { get; set; } = SingletonId;

    [Column("hot_retention_days")]
    public int HotRetentionDays { get; set; } = DefaultHotRetentionDays;

    [Column("warm_retention_days")]
    public int WarmRetentionDays { get; set; } = DefaultWarmRetentionDays;

    /// <summary>RKSV / BAO §132 legal floor for System backups (years).</summary>
    [Column("cold_retention_years")]
    public int ColdRetentionYears { get; set; } = DefaultColdRetentionYears;

    [Column("cold_storage_enabled")]
    public bool ColdStorageEnabled { get; set; }

    [Column("legal_retention_enforced")]
    public bool LegalRetentionEnforced { get; set; } = true;

    [MaxLength(32)]
    [Column("cloud_provider")]
    public string CloudProvider { get; set; } = nameof(CloudStorageProviderKind.Filesystem);

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    [Column("updated_by_user_id")]
    public string? UpdatedByUserId { get; set; }
}
