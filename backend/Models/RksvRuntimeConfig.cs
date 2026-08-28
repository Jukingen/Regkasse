using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Singleton row (Id=1): persisted RKSV presentation overlay (Demo/Production labels).
/// Instance-wide — not tenant-scoped. Appsettings remain the seed/fallback until the first persist.
/// </summary>
[Table("rksv_runtime_config")]
public sealed class RksvRuntimeConfig
{
    public const int SingletonId = 1;

    public const string ModeDemo = "Demo";
    public const string ModeProduction = "Production";
    public const string IntegrationSimulation = "Simulation";
    public const string IntegrationReal = "Real";

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("id")]
    public int Id { get; set; } = SingletonId;

    /// <summary>Demo | Production — fiscal receipt labelling / RKSV.Mode overlay.</summary>
    [Required]
    [MaxLength(32)]
    [Column("mode")]
    public string Mode { get; set; } = ModeProduction;

    /// <summary>Simulation | Real — RKSV.TseMode overlay (presentation + lock evaluation).</summary>
    [Required]
    [MaxLength(32)]
    [Column("tse_mode")]
    public string TseMode { get; set; } = IntegrationReal;

    /// <summary>Simulation | Real — RKSV.FinanzOnlineMode overlay (presentation + lock evaluation).</summary>
    [Required]
    [MaxLength(32)]
    [Column("finanz_online_mode")]
    public string FinanzOnlineMode { get; set; } = IntegrationReal;

    [Column("show_demo_label")]
    public bool ShowDemoLabel { get; set; }

    /// <summary>
    /// Development-only: skip TSE health probes. Ignored when <see cref="TseMode"/> is Real
    /// and ignored outside Development. Overlay wins over <c>DevelopmentOptions:BypassTseInDevelopment</c>.
    /// </summary>
    [Column("bypass_tse_in_development")]
    public bool BypassTseInDevelopment { get; set; }

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_by_user_id")]
    public Guid? UpdatedByUserId { get; set; }

    public bool IsDemoMode =>
        string.Equals(Mode, ModeDemo, StringComparison.OrdinalIgnoreCase);

    public bool IsTseSimulation =>
        string.Equals(TseMode, IntegrationSimulation, StringComparison.OrdinalIgnoreCase);

    public bool IsFinanzOnlineSimulation =>
        string.Equals(FinanzOnlineMode, IntegrationSimulation, StringComparison.OrdinalIgnoreCase);

    public static RksvRuntimeConfig CreateFromAppsettings(
        string mode,
        string tseMode,
        string finanzOnlineMode,
        bool showDemoLabel,
        bool bypassTseInDevelopment = false) =>
        new()
        {
            Id = SingletonId,
            Mode = NormalizeMode(mode),
            TseMode = NormalizeIntegrationMode(tseMode),
            FinanzOnlineMode = NormalizeIntegrationMode(finanzOnlineMode),
            ShowDemoLabel = showDemoLabel,
            BypassTseInDevelopment = bypassTseInDevelopment,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByUserId = null,
        };

    public static string NormalizeMode(string? raw) =>
        string.Equals(raw, ModeDemo, StringComparison.OrdinalIgnoreCase)
            ? ModeDemo
            : ModeProduction;

    public static string NormalizeIntegrationMode(string? raw) =>
        string.Equals(raw, IntegrationSimulation, StringComparison.OrdinalIgnoreCase)
            ? IntegrationSimulation
            : IntegrationReal;

    public static bool IsValidMode(string? raw) =>
        string.Equals(raw, ModeDemo, StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, ModeProduction, StringComparison.OrdinalIgnoreCase);

    public static bool IsValidIntegrationMode(string? raw) =>
        string.Equals(raw, IntegrationSimulation, StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, IntegrationReal, StringComparison.OrdinalIgnoreCase);
}
