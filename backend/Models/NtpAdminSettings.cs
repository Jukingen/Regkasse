using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Singleton runtime row (Id=1): admin-adjustable NTP monitoring options (overrides appsettings defaults when present).
/// </summary>
[Table("ntp_admin_settings")]
public sealed class NtpAdminSettings
{
    public const int SingletonId = 1;

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; } = SingletonId;

    [Column("auto_sync_enabled")]
    public bool AutoSyncEnabled { get; set; } = true;

    [Column("sync_interval_minutes")]
    public int SyncIntervalMinutes { get; set; } = 60;

    [Column("max_allowed_offset_seconds")]
    public int MaxAllowedOffsetSeconds { get; set; } = 5;

    [Column("critical_offset_seconds")]
    public int CriticalOffsetSeconds { get; set; } = 60;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; }
}
