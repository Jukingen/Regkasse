using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class DeploymentComplianceChecklistDto
{
    public bool DepExportTested { get; set; }
    public bool TseSignatureTested { get; set; }
    public bool FinanzOnlineTestSubmission { get; set; }
    public bool NtpTimeSyncChecked { get; set; }
    public bool TenantIsolationVerified { get; set; }
}

public sealed class DeploymentComplianceSignoffRequest
{
    [Required]
    [MaxLength(512)]
    public string ImageTag { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? GitSha { get; set; }

    [MaxLength(32)]
    public string Stage { get; set; } = "production";

    [Required]
    public DeploymentComplianceChecklistDto Checklist { get; set; } = new();

    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>Hours until sign-off expires (default 72).</summary>
    public int? ValidHours { get; set; }
}

public sealed class DeploymentComplianceSignoffDto
{
    public Guid Id { get; set; }
    public string ImageTag { get; set; } = string.Empty;
    public string? GitSha { get; set; }
    public string Stage { get; set; } = string.Empty;
    public DeploymentComplianceChecklistDto Checklist { get; set; } = new();
    public string SignedByUserId { get; set; } = string.Empty;
    public string? SignedByRole { get; set; }
    public string? SignedByDisplayName { get; set; }
    public string? Notes { get; set; }
    public DateTime SignedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsValid { get; set; }
}

public sealed class DeploymentComplianceGateStatusDto
{
    public DateTime CheckedAtUtc { get; set; }
    public string ImageTag { get; set; } = string.Empty;
    public string Stage { get; set; } = "production";
    public bool SignoffPresent { get; set; }
    public bool SignoffValid { get; set; }
    public bool ChecklistComplete { get; set; }
    public bool GatePassed { get; set; }
    public DeploymentComplianceSignoffDto? LatestSignoff { get; set; }
    public IReadOnlyList<string> MissingChecklistItems { get; set; } = Array.Empty<string>();
    public string StrategyDoc { get; set; } = "docs/DEPLOYMENT_COMPLIANCE.md";
}

public sealed class DeploymentAuditPayload
{
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? GitSha { get; set; }
    public string? RunUrl { get; set; }
    public IReadOnlyList<string>? TenantIds { get; set; }
    public string? TriggeredBy { get; set; }
    public string? ErrorMessage { get; set; }
    public bool? SmokePassed { get; set; }
}
