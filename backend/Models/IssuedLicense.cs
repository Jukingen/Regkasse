using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Audit row for every license issued by the admin panel via <c>POST /api/admin/license/generate</c>.
/// Stores both the human-readable REGK display key and the signed RS256 JWT (the actual activation proof);
/// the JWT can be re-sent to a customer who lost the original. Revocation flags are kept for forward compatibility
/// (no enforcement on activation yet — kept here so future logic has a single audit trail to consult).
/// </summary>
[Table("issued_licenses")]
public sealed class IssuedLicense
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display key shown to the customer (format: <c>REGK-XXXXX-XXXXX-XXXXX</c>).</summary>
    [Required]
    [Column("license_key")]
    [MaxLength(64)]
    public string LicenseKey { get; set; } = string.Empty;

    [Required]
    [Column("customer_name")]
    [MaxLength(256)]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Effective expiry instant (UTC). Stored as the JWT <c>exp</c> claim moment.</summary>
    [Required]
    [Column("expiry_at_utc")]
    public DateTime ExpiryAtUtc { get; set; }

    /// <summary>True if the JWT carries a non-empty <c>machineHash</c> claim (machine-bound license).</summary>
    [Required]
    [Column("require_fingerprint")]
    public bool RequireFingerprint { get; set; }

    /// <summary>The fingerprint baked into the JWT when <see cref="RequireFingerprint"/> is true; null/empty for floating.</summary>
    [Column("machine_hash_hex")]
    [MaxLength(128)]
    public string? MachineHashHex { get; set; }

    /// <summary>
    /// RS256 JWT (header.payload.signature) — the activation proof. Sensitive: do not surface in unrelated logs.
    /// </summary>
    [Required]
    [Column("signed_jwt")]
    public string SignedJwt { get; set; } = string.Empty;

    [Required]
    [Column("issued_at_utc")]
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Identity user id of the admin who issued the license. Nullable for system-issued (e.g., scripts).</summary>
    [Column("issued_by_user_id")]
    [MaxLength(450)]
    public string? IssuedByUserId { get; set; }

    [Required]
    [Column("is_revoked")]
    public bool IsRevoked { get; set; }

    [Column("is_cancelled")]
    public bool IsCancelled { get; set; }

    [Column("cancelled_at_utc")]
    public DateTime? CancelledAtUtc { get; set; }

    [Column("cancelled_by_user_id")]
    [MaxLength(450)]
    public string? CancelledByUserId { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("deleted_at_utc")]
    public DateTime? DeletedAtUtc { get; set; }

    [Column("deleted_by_user_id")]
    [MaxLength(450)]
    public string? DeletedByUserId { get; set; }

    [Column("revoked_at_utc")]
    public DateTime? RevokedAtUtc { get; set; }

    [Column("revoked_by_user_id")]
    [MaxLength(450)]
    public string? RevokedByUserId { get; set; }

    [Column("revocation_reason")]
    [MaxLength(512)]
    public string? RevocationReason { get; set; }

    /// <summary>
    /// When set, this row was replaced by <see cref="SupersededByLicense"/> (upgrade/renewal path);
    /// the row is not revoked and remains in the audit trail.
    /// </summary>
    [Column("superseded_by_license_id")]
    public Guid? SupersededByLicenseId { get; set; }

    [ForeignKey(nameof(SupersededByLicenseId))]
    public IssuedLicense? SupersededByLicense { get; set; }

    /// <summary>
    /// When set, this row chain-continues to another issuance (<c>POST /api/admin/license/transfer</c>);
    /// the replacement row carries the new machine binding and JWT. Not revoked — audit only.
    /// </summary>
    [Column("transferred_to_license_id")]
    public Guid? TransferredToLicenseId { get; set; }

    /// <summary>JSON array of <see cref="LicenseFeatureIds"/> strings; null means full default bundle (backward compatible).</summary>
    [Column("features_json")]
    public string? FeaturesJson { get; set; }
}
