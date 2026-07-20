using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Custom / vanity domain for a tenant website or customer app.
/// Platform subdomain remains <see cref="Tenant.Slug"/> (e.g. cafe.regkasse.at).
/// </summary>
[Table("tenant_domains")]
public class TenantDomain : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    /// <summary>Custom apex or FQDN, e.g. <c>cafe-muster.at</c> or <c>www.cafe-muster.at</c>.</summary>
    [Required]
    [MaxLength(253)]
    [Column("domain")]
    public string Domain { get; set; } = string.Empty;

    /// <summary>Platform subdomain label (usually matches <see cref="Tenant.Slug"/>).</summary>
    [Required]
    [MaxLength(64)]
    [Column("subdomain")]
    public string Subdomain { get; set; } = string.Empty;

    [Column("is_verified")]
    public bool IsVerified { get; set; }

    /// <summary>DNS TXT / HTTP token for ownership proof (not a secret for auth).</summary>
    [Required]
    [MaxLength(128)]
    [Column("verification_token")]
    public string VerificationToken { get; set; } = string.Empty;

    [Column("verified_at")]
    public DateTime? VerifiedAt { get; set; }

    /// <summary>When false, domain is ignored for host routing and public site publish.</summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Preferred domain for public website links when multiple verified domains exist.</summary>
    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
