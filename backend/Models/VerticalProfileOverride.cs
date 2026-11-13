using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Database overlay for a POS vertical profile. Code seeds stay the default.
/// When a row exists and is not deleted, its columns replace the seed (database wins).
/// A row whose id is not a code seed is a custom profile.
/// </summary>
[Table("vertical_profile_overrides")]
public sealed class VerticalProfileOverride
{
    [Key]
    [MaxLength(64)]
    [Column("profile_id")]
    public string ProfileId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("pos_features_json", TypeName = "jsonb")]
    public string PosFeaturesJson { get; set; } = "{}";

    [Required]
    [Column("required_fields_json", TypeName = "jsonb")]
    public string RequiredFieldsJson { get; set; } = """{"customer":[],"product":[],"order":[]}""";

    [Required]
    [Column("optional_fields_json", TypeName = "jsonb")]
    public string OptionalFieldsJson { get; set; } = """{"customer":[],"product":[],"order":[]}""";

    [Required]
    [MaxLength(32)]
    [Column("pos_layout")]
    public string PosLayout { get; set; } = VerticalProfileLayouts.Standard;

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
