namespace KasseAPI_Final.Models;

/// <summary>Platform deployment status row reported by CI (not tenant-scoped).</summary>
public sealed class DeploymentRun
{
    public Guid Id { get; set; }

    /// <summary>staging | canary | production</summary>
    public string Stage { get; set; } = string.Empty;

    /// <summary>pending | deploying | smoke_running | succeeded | failed | rolled_back</summary>
    public string Status { get; set; } = string.Empty;

    public string? GitSha { get; set; }

    public string? GitRef { get; set; }

    public string? ImageTag { get; set; }

    /// <summary>Comma-separated tenant slugs/UUIDs (canary); null for full-stage deploys.</summary>
    public string? TenantIdsJson { get; set; }

    public string? ErrorMessage { get; set; }

    public string? RunUrl { get; set; }

    public string? TriggeredBy { get; set; }

    /// <summary>Last smoke result: true/false; null if not run.</summary>
    public bool? SmokePassed { get; set; }

    /// <summary>Pipe-separated smoke check summary (PASS:/FAIL:/SKIP:).</summary>
    public string? SmokeSummary { get; set; }

    /// <summary>Previous image for manual rollback (optional).</summary>
    public string? PreviousImageTag { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
