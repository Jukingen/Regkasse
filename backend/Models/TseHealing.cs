using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

public static class TseHealingConditions
{
    public const string HealthBelowDegraded = "HealthBelowDegraded";
    public const string DeviceOffline = "DeviceOffline";
    public const string PrimaryUnhealthyWithBackup = "PrimaryUnhealthyWithBackup";
    public const string TransientErrorPresent = "TransientErrorPresent";
}

public static class TseHealingActions
{
    public const string ReprobeHealth = "ReprobeHealth";
    public const string ClearTransientError = "ClearTransientError";
    public const string AttemptFailover = "AttemptFailover";
}

public static class TseHealingRuleStatuses
{
    public const string Enabled = "Enabled";
    public const string Disabled = "Disabled";
}

public static class TseHealingAttemptStatuses
{
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
    public const string DiagnosedOnly = "DiagnosedOnly";
    public const string Cooldown = "Cooldown";
}

/// <summary>Per-tenant TSE auto-healing policy (operational; does not rewrite fiscal chains).</summary>
[Table("tse_healing_configurations")]
public sealed class TseHealingConfiguration
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; }

    [Column("max_auto_heal_attempts")]
    public int MaxAutoHealAttempts { get; set; } = 3;

    [Column("cooldown_minutes")]
    public int CooldownMinutes { get; set; } = 30;

    [Column("notify_on_heal")]
    public bool NotifyOnHeal { get; set; } = true;

    /// <summary>
    /// When true, matching rules may call <c>ITseFailoverService.CheckAndFailoverAsync</c>.
    /// Default false — diagnose / re-probe only unless Super Admin opts in.
    /// </summary>
    [Column("allow_auto_failover")]
    public bool AllowAutoFailover { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(450)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    public ICollection<TseHealingRule> Rules { get; set; } = new List<TseHealingRule>();
}

[Table("tse_healing_rules")]
public sealed class TseHealingRule
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("configuration_id")]
    public Guid ConfigurationId { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("condition")]
    public string Condition { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("action")]
    public string Action { get; set; } = string.Empty;

    [Column("priority")]
    public int Priority { get; set; } = 100;

    [Required]
    [MaxLength(24)]
    [Column("status")]
    public string Status { get; set; } = TseHealingRuleStatuses.Enabled;

    [Column("last_triggered_at")]
    public DateTime? LastTriggeredAt { get; set; }

    [ForeignKey(nameof(ConfigurationId))]
    public TseHealingConfiguration? Configuration { get; set; }
}

[Table("tse_healing_history")]
public sealed class TseHealingHistoryEntry
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("device_id")]
    public Guid DeviceId { get; set; }

    [MaxLength(64)]
    [Column("condition")]
    public string? Condition { get; set; }

    [MaxLength(64)]
    [Column("action")]
    public string? Action { get; set; }

    [Required]
    [MaxLength(24)]
    [Column("status")]
    public string Status { get; set; } = TseHealingAttemptStatuses.DiagnosedOnly;

    [Column("applied")]
    public bool Applied { get; set; }

    [Column("health_score_before")]
    public int HealthScoreBefore { get; set; }

    [Column("health_score_after")]
    public int? HealthScoreAfter { get; set; }

    [MaxLength(2000)]
    [Column("message")]
    public string? Message { get; set; }

    [Column("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [MaxLength(450)]
    [Column("actor_user_id")]
    public string? ActorUserId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    [ForeignKey(nameof(DeviceId))]
    public TseDevice? Device { get; set; }
}
