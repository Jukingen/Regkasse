using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Per-tenant deployment tracking for canary / progressive rollouts.
/// Table: <c>deployment_history</c> (snake_case columns).
/// </summary>
[Table("deployment_history")]
public sealed class TenantDeploymentHistory
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>Image tag or release version (e.g. sha-abcdef1 / v1.2.3).</summary>
    [Required]
    [MaxLength(512)]
    [Column("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>Previous version before this deploy (for rollback).</summary>
    [MaxLength(512)]
    [Column("previous_version")]
    public string? PreviousVersion { get; set; }

    /// <summary>staging | canary | production</summary>
    [Required]
    [MaxLength(32)]
    [Column("stage")]
    public string Stage { get; set; } = string.Empty;

    /// <summary>
    /// pending | deploying | succeeded | failed | rolled_back | canary_soak | promoted
    /// </summary>
    [Required]
    [MaxLength(32)]
    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [MaxLength(64)]
    [Column("git_sha")]
    public string? GitSha { get; set; }

    [MaxLength(1024)]
    [Column("run_url")]
    public string? RunUrl { get; set; }

    [MaxLength(200)]
    [Column("triggered_by")]
    public string? TriggeredBy { get; set; }

    [MaxLength(2000)]
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("smoke_passed")]
    public bool? SmokePassed { get; set; }

    [Column("deployed_at_utc")]
    public DateTime DeployedAtUtc { get; set; }

    [Column("soak_until_utc")]
    public DateTime? SoakUntilUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; }
}
