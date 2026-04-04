using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Minimal user↔tenant link. At most one <see cref="IsActive"/> row per user (partial unique index).
/// Phase: single effective tenant; no switch UI; no organization hierarchy.
/// </summary>
[Table("user_tenant_memberships")]
public class UserTenantMembership
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>When false, row is historical; not counted for login tenant resolution.</summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime? UpdatedAtUtc { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser? User { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }
}
