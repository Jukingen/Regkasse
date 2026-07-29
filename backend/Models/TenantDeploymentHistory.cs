namespace KasseAPI_Final.Models;

/// <summary>
/// Per-tenant deployment tracking for canary / progressive rollouts.
/// Table: <c>deployment_history</c>.
/// </summary>
public sealed class TenantDeploymentHistory
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>Image tag or release version (e.g. sha-abcdef1 / v1.2.3).</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Previous version before this deploy (for rollback).</summary>
    public string? PreviousVersion { get; set; }

    /// <summary>staging | canary | production</summary>
    public string Stage { get; set; } = string.Empty;

    /// <summary>
    /// pending | deploying | succeeded | failed | rolled_back | canary_soak | promoted
    /// </summary>
    public string Status { get; set; } = string.Empty;

    public string? GitSha { get; set; }

    public string? RunUrl { get; set; }

    public string? TriggeredBy { get; set; }

    public string? ErrorMessage { get; set; }

    public bool? SmokePassed { get; set; }

    public DateTime DeployedAtUtc { get; set; }

    public DateTime? SoakUntilUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
