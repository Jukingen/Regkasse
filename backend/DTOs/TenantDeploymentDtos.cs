using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class TenantDeploymentHistoryDto
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string? TenantSlug { get; set; }

    public string? TenantName { get; set; }

    public string Version { get; set; } = string.Empty;

    public string? PreviousVersion { get; set; }

    public string Stage { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? GitSha { get; set; }

    public string? RunUrl { get; set; }

    public string? TriggeredBy { get; set; }

    public string? ErrorMessage { get; set; }

    public bool? SmokePassed { get; set; }

    public DateTime DeployedAtUtc { get; set; }

    public DateTime? SoakUntilUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>True when stage=canary and soak window still open.</summary>
    public bool IsCanarySoaking { get; set; }
}

public sealed class DeploymentOverallStatusDto
{
    public DateTime CheckedAtUtc { get; set; }

    public IReadOnlyList<TenantDeploymentHistoryDto> Tenants { get; set; }
        = Array.Empty<TenantDeploymentHistoryDto>();

    public int CanarySoakingCount { get; set; }

    public int FailedCount { get; set; }

    public string? RecommendedNextCanaryTenantSlug { get; set; }

    public string StrategyDoc { get; set; } = "docs/CANARY_DEPLOYMENT.md";
}

public sealed class TenantDeploymentRecordRequest
{
    [Required]
    public string TenantIdOrSlug { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string Version { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? PreviousVersion { get; set; }

    [Required]
    [MaxLength(32)]
    public string Stage { get; set; } = "canary";

    [Required]
    [MaxLength(32)]
    public string Status { get; set; } = "succeeded";

    public string? GitSha { get; set; }

    public string? RunUrl { get; set; }

    public string? TriggeredBy { get; set; }

    public string? ErrorMessage { get; set; }

    public bool? SmokePassed { get; set; }

    /// <summary>Hours to keep canary_soak (default from config, typically 24–48).</summary>
    public int? SoakHours { get; set; }
}

public sealed class TenantDeploymentRollbackRequest
{
    /// <summary>Must be exactly <c>rollback</c>.</summary>
    [Required]
    [MaxLength(32)]
    public string Confirm { get; set; } = string.Empty;

    public string? PreviousVersion { get; set; }
}
