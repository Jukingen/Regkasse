using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Outcome of a single license activation API attempt (success, failure, or later revocation).</summary>
public enum LicenseActivationAttemptStatus
{
    Success = 0,
    Failed = 1,
    Revoked = 2,
}

/// <summary>Append-only audit of POST /api/admin/license/activate attempts (full key in DB; logs remain masked).</summary>
[Table("license_activation_attempts")]
public sealed class LicenseActivationAttempt
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("license_key")]
    [MaxLength(64)]
    public string LicenseKey { get; set; } = "";

    [Required]
    [Column("machine_fingerprint")]
    [MaxLength(128)]
    public string MachineFingerprint { get; set; } = "";

    [Required]
    [Column("activation_status")]
    public LicenseActivationAttemptStatus ActivationStatus { get; set; }

    [Column("failure_reason")]
    [MaxLength(4000)]
    public string? FailureReason { get; set; }

    [Column("client_ip")]
    [MaxLength(45)]
    public string? ClientIp { get; set; }

    [Column("user_agent")]
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [Required]
    [Column("activated_at_utc")]
    public DateTime ActivatedAtUtc { get; set; }

    [Column("deactivated_at_utc")]
    public DateTime? DeactivatedAtUtc { get; set; }
}
