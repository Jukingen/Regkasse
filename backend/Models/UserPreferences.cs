using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Per-user admin panel UI preferences (FA theme, density, defaults). Not tenant-scoped.
/// </summary>
[Table("user_preferences")]
public class UserPreferences
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [StringLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    [Column("theme_mode")]
    public string ThemeMode { get; set; } = "system";

    [Required]
    [StringLength(20)]
    [Column("density_mode")]
    public string DensityMode { get; set; } = "standard";

    [Required]
    [StringLength(200)]
    [Column("default_page")]
    public string DefaultPage { get; set; } = "/dashboard";

    [StringLength(20)]
    [Column("date_format")]
    public string? DateFormat { get; set; }

    [StringLength(10)]
    [Column("time_format")]
    public string? TimeFormat { get; set; }

    /// <summary>IANA timezone id used for FA date/time display (e.g. Europe/Vienna).</summary>
    [StringLength(64)]
    [Column("time_zone")]
    public string? TimeZone { get; set; }

    /// <summary>Admin UI text locale: de | en | tr.</summary>
    [StringLength(10)]
    [Column("language")]
    public string? Language { get; set; }

    [Column("reduced_animations")]
    public bool ReducedAnimations { get; set; }

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
