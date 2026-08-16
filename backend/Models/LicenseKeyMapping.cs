using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Maps a legacy REGK display key to the unified <c>REGK-yyyyMMdd-{slug}-{8}</c> key
/// so printed / JWT-bound keys keep working after <c>MigrateLicensesToUnifiedFormat</c>.
/// </summary>
[Table("license_key_mappings")]
public sealed class LicenseKeyMapping
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("old_license_key")]
    [MaxLength(100)]
    public string OldLicenseKey { get; set; } = string.Empty;

    [Required]
    [Column("new_license_key")]
    [MaxLength(100)]
    public string NewLicenseKey { get; set; } = string.Empty;

    /// <summary><see cref="LicenseKeyKinds"/>: system, tenant, or both.</summary>
    [Required]
    [Column("license_kind")]
    [MaxLength(16)]
    public string LicenseKind { get; set; } = LicenseKeyKinds.Tenant;

    [Required]
    [Column("source_table")]
    [MaxLength(32)]
    public string SourceTable { get; set; } = string.Empty;

    [Column("source_id")]
    public Guid? SourceId { get; set; }

    [Required]
    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
