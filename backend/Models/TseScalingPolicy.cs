using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Per-tenant TSE auto-scaling policy (operational; not fiscal).
/// Soft device changes only when <see cref="AutoProvision"/> is true — default is recommend-only.
/// </summary>
[Table("tse_scaling_policies")]
public sealed class TseScalingPolicy
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; }

    [Column("min_devices")]
    public int MinDevices { get; set; } = 1;

    [Column("max_devices")]
    public int MaxDevices { get; set; } = 5;

    [Column("target_transactions_per_device")]
    public int TargetTransactionsPerDevice { get; set; } = 5000;

    /// <summary>Scale-up when load % exceeds this (default 80).</summary>
    [Column("scale_up_threshold")]
    public double ScaleUpThreshold { get; set; } = 80;

    /// <summary>Scale-down when load % is below this (default 30).</summary>
    [Column("scale_down_threshold")]
    public double ScaleDownThreshold { get; set; } = 30;

    [Column("cooldown_minutes")]
    public int CooldownMinutes { get; set; } = 30;

    /// <summary>
    /// When true, EvaluateAndScale may soft-create/deactivate backup device stubs.
    /// Never provisions live cloud TSS / Startbeleg. Default false = recommendation only.
    /// </summary>
    [Column("auto_provision")]
    public bool AutoProvision { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(450)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
}

/// <summary>Append-only TSE auto-scaling evaluation / action history.</summary>
[Table("tse_scaling_history")]
public sealed class TseScalingHistoryEntry
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("evaluated_at")]
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    /// <summary><see cref="TseScalingActions"/>.</summary>
    [Required]
    [MaxLength(32)]
    [Column("action")]
    public string Action { get; set; } = TseScalingActions.NoOp;

    [Column("from_devices")]
    public int FromDevices { get; set; }

    [Column("to_devices")]
    public int ToDevices { get; set; }

    [Column("load_percent")]
    public double LoadPercent { get; set; }

    [Column("applied")]
    public bool Applied { get; set; }

    [Column("simulation_only")]
    public bool SimulationOnly { get; set; } = true;

    [MaxLength(1000)]
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(450)]
    [Column("actor_user_id")]
    public string? ActorUserId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
}

public static class TseScalingActions
{
    public const string ScaleUp = "ScaleUp";
    public const string ScaleDown = "ScaleDown";
    public const string Recommend = "Recommend";
    public const string NoOp = "NoOp";
    public const string SkippedCooldown = "SkippedCooldown";
    public const string Disabled = "Disabled";
}
