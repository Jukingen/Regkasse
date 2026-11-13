namespace KasseAPI_Final.Models;

/// <summary>
/// Compliance officer sign-off for a production (or canary→prod) image promotion.
/// Table: <c>deployment_compliance_signoffs</c>.
/// </summary>
public sealed class DeploymentComplianceSignoff
{
    public Guid Id { get; set; }

    /// <summary>Image tag being approved (e.g. sha-abcdef1).</summary>
    public string ImageTag { get; set; } = string.Empty;

    public string? GitSha { get; set; }

    /// <summary>Target stage — typically production.</summary>
    public string Stage { get; set; } = "production";

    /// <summary>JSON checklist answers (depExport, tseSignature, finanzOnline, ntp, tenantIsolation).</summary>
    public string ChecklistJson { get; set; } = "{}";

    public string SignedByUserId { get; set; } = string.Empty;

    public string? SignedByRole { get; set; }

    public string? SignedByDisplayName { get; set; }

    public string? Notes { get; set; }

    public DateTime SignedAtUtc { get; set; }

    /// <summary>Optional expiry; CI rejects sign-offs older than configured max age.</summary>
    public DateTime? ExpiresAtUtc { get; set; }
}
