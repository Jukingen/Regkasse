using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// SaaS tenant root. Wave 0–1: single seeded legacy row; no membership model yet.
/// </summary>
[Table("tenants")]
public class Tenant : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Stable external key (e.g. <c>default</c> for legacy single-tenant deployments).</summary>
    [Required]
    [MaxLength(64)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("email")]
    public string? Email { get; set; }

    [MaxLength(50)]
    [Column("phone")]
    public string? Phone { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    /// <summary><see cref="TenantStatuses"/> value.</summary>
    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = TenantStatuses.Active;

    [MaxLength(100)]
    [Column("license_key")]
    public string? LicenseKey { get; set; }

    [Column("license_valid_until_utc")]
    public DateTime? LicenseValidUntilUtc { get; set; }

    /// <summary>UTC instant when the post-expiry grace window started (set on first day after expiry).</summary>
    [Column("license_grace_period_started_at")]
    public DateTime? LicenseGracePeriodStartedAt { get; set; }

    /// <summary>Consumed grace days for renewal deduction and reporting.</summary>
    [Column("license_grace_period_used_days")]
    public int LicenseGracePeriodUsedDays { get; set; }

    /// <summary>Active Mandanten billing sale row when provisioned via Super Admin license sales.</summary>
    [Column("current_license_sale_id")]
    public Guid? CurrentLicenseSaleId { get; set; }

    [ForeignKey(nameof(CurrentLicenseSaleId))]
    public virtual LicenseSale? CurrentLicenseSale { get; set; }

    [Column("last_license_activation_utc")]
    public DateTime? LastLicenseActivationUtc { get; set; }

    [Column("license_activation_count")]
    public int LicenseActivationCount { get; set; }

    [Column("deleted_at_utc")]
    public DateTime? DeletedAtUtc { get; set; }

    [MaxLength(450)]
    [Column("deleted_by_user_id")]
    public string? DeletedByUserId { get; set; }
}
