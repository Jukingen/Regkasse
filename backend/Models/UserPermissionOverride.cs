using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// User-level permission grant or deny override. Applied on top of role permissions at login/JWT build.
/// <see cref="TenantId"/> null = global override; otherwise scoped to a single tenant context.
/// </summary>
[Table("user_permission_overrides")]
public class UserPermissionOverride
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>When null, applies in every tenant context for the user.</summary>
    [Column("tenant_id")]
    public Guid? TenantId { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("permission")]
    public string Permission { get; set; } = string.Empty;

    /// <summary>true = grant; false = deny (removes even when role grants it).</summary>
    [Column("is_granted")]
    public bool IsGranted { get; set; }

    [MaxLength(500)]
    [Column("reason")]
    public string? Reason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    [Column("created_by_user_id")]
    public string? CreatedByUserId { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser? User { get; set; }
}
