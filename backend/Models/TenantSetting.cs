using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Key/value tenant (or global) settings. Feature-flag overrides use keys
/// <c>FeatureFlags:{Name}</c> with values <c>true</c>/<c>false</c>.
/// <see cref="TenantId"/> null = global override (Super Admin).
/// </summary>
[Table("tenant_settings")]
public sealed class TenantSetting
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Null = deployment-wide override; set = mandant-specific.</summary>
    [Column("tenant_id")]
    public Guid? TenantId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("key")]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    [Column("value")]
    public string Value { get; set; } = string.Empty;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    [Column("updated_by_user_id")]
    public string? UpdatedByUserId { get; set; }
}
