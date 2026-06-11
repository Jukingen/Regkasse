using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Singleton row (Id=1): persisted development-mode toggles. Effective bypass flags require a Development host environment.
/// </summary>
[Table("development_mode_settings")]
public sealed class DevelopmentModeSettings
{
    public const int SingletonId = 1;

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("id")]
    public int Id { get; set; } = SingletonId;

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    [Column("bypass_license")]
    public bool BypassLicense { get; set; } = true;

    [Column("bypass_ntp_check")]
    public bool BypassNtpCheck { get; set; } = true;

    [Column("bypass_tse_check")]
    public bool BypassTseCheck { get; set; } = true;

    [Column("simulate_offline")]
    public bool SimulateOffline { get; set; } = false;

    [Column("force_online")]
    public bool ForceOnline { get; set; } = true;

    [Column("valid_days")]
    public int ValidDays { get; set; } = 365;

    [Column("features", TypeName = "jsonb")]
    public string[] Features { get; set; } = [];

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_by_user_id")]
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>Singleton seed / first-run defaults for unrestricted local development.</summary>
    public static DevelopmentModeSettings CreateDefaultSingleton() =>
        new()
        {
            Id = SingletonId,
            Enabled = true,
            BypassLicense = true,
            BypassNtpCheck = true,
            BypassTseCheck = true,
            SimulateOffline = false,
            ForceOnline = true,
            ValidDays = 365,
            Features = [],
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByUserId = null,
        };
}
