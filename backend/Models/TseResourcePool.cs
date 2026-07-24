using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Super Admin TSE resource pool for multi-tenant capacity planning (platform-scoped, not fiscal).
/// </summary>
[Table("tse_resource_pools")]
public class TseResourcePool
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(120)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary><see cref="TseResourcePoolTypes"/>: Shared, Dedicated, Hybrid.</summary>
    [Required]
    [MaxLength(32)]
    [Column("pool_type")]
    public string PoolType { get; set; } = TseResourcePoolTypes.Shared;

    /// <summary>Total capacity units (device / signing slots) available in this pool.</summary>
    [Column("total_capacity")]
    public int TotalCapacity { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(450)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    public virtual ICollection<TseResourcePoolAssignment> Assignments { get; set; } =
        new List<TseResourcePoolAssignment>();

    public virtual ICollection<TseResourcePoolRule> Rules { get; set; } =
        new List<TseResourcePoolRule>();
}

/// <summary>Tenant membership in a TSE resource pool (one active pool per tenant).</summary>
[Table("tse_resource_pool_assignments")]
public class TseResourcePoolAssignment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("pool_id")]
    public Guid PoolId { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>Reserved capacity units for this tenant (default 1).</summary>
    [Column("reserved_capacity")]
    public int ReservedCapacity { get; set; } = 1;

    [Column("assigned_at")]
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    [Column("assigned_by")]
    public string? AssignedBy { get; set; }

    [ForeignKey(nameof(PoolId))]
    public virtual TseResourcePool? Pool { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }
}

/// <summary>Configurable rule attached to a TSE resource pool.</summary>
[Table("tse_resource_pool_rules")]
public class TseResourcePoolRule
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("pool_id")]
    public Guid PoolId { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("rule_type")]
    public string RuleType { get; set; } = string.Empty;

    [MaxLength(256)]
    [Column("rule_value")]
    public string? RuleValue { get; set; }

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [ForeignKey(nameof(PoolId))]
    public virtual TseResourcePool? Pool { get; set; }
}

public static class TseResourcePoolTypes
{
    public const string Shared = "Shared";
    public const string Dedicated = "Dedicated";
    public const string Hybrid = "Hybrid";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Shared, Dedicated, Hybrid,
    };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());
}

public static class TseResourcePoolRuleTypes
{
    public const string MaxTenants = "MaxTenants";
    public const string MinHealthScore = "MinHealthScore";
    public const string AllowSoftTse = "AllowSoftTse";
    public const string PreferProvider = "PreferProvider";
}
