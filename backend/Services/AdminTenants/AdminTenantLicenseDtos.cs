using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed record TenantLicenseStatusDto(
    string Kind,
    string? LicenseKey,
    DateTime? ValidUntilUtc,
    int? DaysRemaining,
    string? Tier,
    IReadOnlyList<string> Features);

public sealed record TenantLicenseHistoryItemDto(
    Guid? IssuedLicenseId,
    string EventType,
    DateTime AtUtc,
    string Summary,
    string? LicenseKey);

public sealed record TenantLicenseOverviewDto(
    TenantLicenseStatusDto Status,
    IReadOnlyList<TenantLicenseHistoryItemDto> History);

public sealed class ExtendTenantLicenseRequest
{
    /// <summary>REGK display key; when set, expiry is taken from issued_licenses when found.</summary>
    [MaxLength(64)]
    public string? LicenseKey { get; set; }

    public DateTime? ValidUntilUtc { get; set; }
}

public sealed class SetTenantLicenseTierRequest
{
    /// <summary>basic | standard | premium</summary>
    [Required]
    [MaxLength(32)]
    public string Tier { get; set; } = string.Empty;

    /// <summary>Optional explicit end date; otherwise extends 365 days from max(now, current end).</summary>
    public DateTime? ValidUntilUtc { get; set; }
}

/// <summary>Result of mandant vs deployment license (<c>issued_licenses</c>) consistency check.</summary>
public sealed record TenantLicenseConsistencyDto(
    bool IsConsistent,
    IReadOnlyList<string> Warnings,
    DateTime? TenantValidUntilUtc,
    Guid? MatchedIssuedLicenseId,
    string? MatchedLicenseKey,
    DateTime? IssuedExpiryAtUtc,
    bool CanIssueDeploymentLicense);

/// <summary>Creates a floating deployment JWT row linked to the tenant mandant license.</summary>
public sealed record TenantLicenseIssueDeploymentResultDto(
    bool Success,
    string? Message,
    string? LicenseKey,
    Guid? IssuedLicenseId,
    TenantLicenseOverviewDto? Overview);

public sealed record TenantLicenseReminderResultDto(
    bool Success,
    string RecipientEmail,
    string? Message);
