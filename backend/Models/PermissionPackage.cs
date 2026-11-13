using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

[Table("permission_packages")]
public class PermissionPackage
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(64)]
    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("is_system")]
    public bool IsSystem { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    [Column("created_by_user_id")]
    public string? CreatedByUserId { get; set; }

    public virtual ICollection<PermissionPackageKey> Keys { get; set; } = new List<PermissionPackageKey>();
    public virtual ICollection<RolePermissionPackage> RoleAssignments { get; set; } = new List<RolePermissionPackage>();
}

[Table("permission_package_keys")]
public class PermissionPackageKey
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("package_id")]
    public Guid PackageId { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("permission")]
    public string Permission { get; set; } = string.Empty;

    [ForeignKey(nameof(PackageId))]
    public virtual PermissionPackage? Package { get; set; }
}

/// <summary>Join: custom Identity role ↔ permission package.</summary>
[Table("role_permission_packages")]
public class RolePermissionPackage
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(450)]
    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    [Column("package_id")]
    public Guid PackageId { get; set; }

    [Column("assigned_at")]
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    [Column("assigned_by_user_id")]
    public string? AssignedByUserId { get; set; }

    [ForeignKey(nameof(PackageId))]
    public virtual PermissionPackage? Package { get; set; }
}
