using System.ComponentModel.DataAnnotations;
using System.Globalization;
using KasseAPI_Final.Models;

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
    string? LicenseKey,
    string? ActorUserId = null,
    string? ActorDisplayName = null);

public sealed record TenantLicenseOverviewDto(
    TenantLicenseStatusDto Status,
    IReadOnlyList<TenantLicenseHistoryItemDto> History);

public sealed class ExtendTenantLicenseRequest
{
    /// <summary>
    /// Mandant billing key (<c>license_sales</c>) for Managers; deployment REGK key for Super Admin when omitted <see cref="ValidUntilUtc"/>.
    /// </summary>
    [MaxLength(100)]
    public string? LicenseKey { get; set; }

    /// <summary>Super Admin override only; Managers must omit (validity comes from the billing or issued key).</summary>
    public DateTime? ValidUntilUtc { get; set; }
}

public sealed class PreviewTenantLicenseRequest
{
    [Required]
    [MaxLength(100)]
    public string LicenseKey { get; set; } = string.Empty;
}

/// <summary>Outcome of resolving a REGK key against <c>issued_licenses</c>.</summary>
public sealed record IssuedLicenseResolveResult(
    IssuedLicense? Issued,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>Outcome of resolving a billing key against <c>license_sales</c> (Manager mandant activation).</summary>
public sealed record BillingLicenseSaleResolveResult(
    LicenseSale? Sale,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>Read-only preview of a REGK license key before extend confirmation.</summary>
public sealed record LicensePreviewResult(
    bool Valid,
    string? LicenseKey,
    DateTime? ValidFromUtc,
    DateTime? ValidUntilUtc,
    int? DurationDays,
    string? DurationDisplay,
    string? Status,
    string? PlanName,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>Result of <c>POST …/license/extend</c> and <c>POST /api/admin/license/mandant/extend</c>.</summary>
public sealed record ExtendTenantLicenseResultDto(
    bool Success,
    string LicenseKey,
    DateTime ValidUntilUtc,
    string Status,
    string Message)
{
    public static ExtendTenantLicenseResultDto FromOverview(TenantLicenseOverviewDto overview)
    {
        var status = overview.Status;
        var validUntil = status.ValidUntilUtc ?? DateTime.UtcNow;
        var utc = DateTime.SpecifyKind(validUntil, DateTimeKind.Utc);
        var dateLabel = utc.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        return new ExtendTenantLicenseResultDto(
            Success: true,
            LicenseKey: status.LicenseKey ?? string.Empty,
            ValidUntilUtc: utc,
            Status: status.Kind,
            Message: $"License extended until {dateLabel}.");
    }
}

public sealed class RenewTenantLicenseRequest
{
    [Range(1, 120)]
    public int AdditionalMonths { get; set; }

    /// <summary>Manager self-service renewal requires explicit payment acknowledgement.</summary>
    public bool PaymentConfirmed { get; set; }
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

/// <summary>Super Admin license inventory row for <c>GET /api/admin/tenants/license-overview</c>.</summary>
public sealed record TenantLicenseOverviewListItemDto(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    string? LicenseKey,
    DateTime? ValidUntilUtc,
    string Status,
    bool HasOwnerAdmin,
    DateTime CreatedAt);
