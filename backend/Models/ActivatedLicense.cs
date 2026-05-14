using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Deployment-local activation record: which REGK key is active for which machine fingerprint until when.
/// Survives process restarts and complements the encrypted on-disk license file.
/// </summary>
[Table("activated_licenses")]
public sealed class ActivatedLicense
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("license_key")]
    [MaxLength(64)]
    public string LicenseKey { get; set; } = string.Empty;

    [Column("customer_name")]
    [MaxLength(256)]
    public string? CustomerName { get; set; }

    [Required]
    [Column("valid_until_utc")]
    public DateTime ValidUntilUtc { get; set; }

    /// <summary>SHA-256 machine hash (hex, lowercase); matches the host fingerprint. Null = not machine-bound.</summary>
    [Column("machine_fingerprint")]
    [MaxLength(128)]
    public string? MachineFingerprint { get; set; }

    [Required]
    [Column("activated_at_utc")]
    public DateTime ActivatedAtUtc { get; set; }

    /// <summary>Last time this deployment reported license use via POS/FA API (middleware validation).</summary>
    [Required]
    [Column("last_seen_at_utc")]
    public DateTime LastSeenAtUtc { get; set; }

    /// <summary>False when superseded by another activation on the same machine or administratively cleared.</summary>
    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Optional user who performed activation (admin UI).</summary>
    [Column("created_by_user_id")]
    public Guid? CreatedByUserId { get; set; }

    /// <summary>JSON array of enabled <see cref="LicenseFeatureIds"/> at activation time; null = full bundle.</summary>
    [Column("features_json")]
    public string? FeaturesJson { get; set; }
}
