using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.Services.LicenseTest;

public sealed record LicenseTestTenantStatusDto(
    Guid TenantId,
    string Slug,
    string Name,
    string? LicenseKey,
    DateTime? ValidUntilUtc,
    string Status,
    int DaysRemaining,
    int DaysOverdue,
    bool IsActive,
    bool IsInGracePeriod,
    bool CanAccess,
    bool CanTransact,
    string StatusMessage);

public sealed record LicenseTestDeploymentStatusDto(
    bool IsValid,
    bool IsTrial,
    bool IsExpired,
    int DaysRemaining,
    DateTime? ExpiryDateUtc,
    string? LicenseKey,
    bool IsDevelopmentBypass,
    string Mode);

public sealed record LicenseTestSnapshotDto(
    LicenseTestTenantStatusDto? Tenant,
    LicenseTestDeploymentStatusDto Deployment,
    bool DevelopmentModeBypassActive,
    DateTime RefreshedAtUtc);

public class LicenseTestSetExpiryRequest
{
    public DateTime? ValidUntilUtc { get; set; }

    /// <summary>When true, sets expiry to one day ago (ignores <see cref="ValidUntilUtc"/>).</summary>
    public bool SetExpired { get; set; }

    /// <summary>When true, sets expiry to 30 days from now (ignores <see cref="ValidUntilUtc"/>).</summary>
    public bool SetActive { get; set; }
}

public sealed class LicenseTestTenantRequest : LicenseTestSetExpiryRequest
{
    [Required]
    public Guid TenantId { get; set; }
}

public enum LicenseTestScenario
{
    Days1,
    Days7,
    Days30,
    Expired,
}

public enum LicenseTestScope
{
    Tenant,
    Deployment,
    Both,
}

public sealed class LicenseTestScenarioRequest
{
    public Guid? TenantId { get; set; }

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LicenseTestScope Scope { get; set; }

    [Required]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LicenseTestScenario Scenario { get; set; }
}

/// <summary>Unified dev-only tenant license update (<c>POST /api/admin/license/test/update</c>).</summary>
public sealed class LicenseTestRequest
{
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>Maps to <c>tenants.license_valid_until_utc</c> (UTC).</summary>
    public DateTime? ValidUntil { get; set; }
}
