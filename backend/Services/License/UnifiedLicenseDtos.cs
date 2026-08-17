using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.License;

/// <summary>
/// Key-based validation result for <see cref="IUnifiedLicenseService"/>.
/// Distinct from <see cref="Models.LicenseValidationResult"/> (deployment snapshot used by middleware).
/// </summary>
public sealed class LicenseKeyValidationResult
{
    public bool IsValid { get; init; }

    public bool IsFormatValid { get; init; }

    public bool ExistsInDatabase { get; init; }

    public bool IsExpired { get; init; }

    public bool SlugMatches { get; init; } = true;

    public string? LicenseKind { get; init; }

    public string? CanonicalLicenseKey { get; init; }

    public string? Slug { get; init; }

    public DateTime? EncodedValidUntilUtc { get; init; }

    public DateTime? DatabaseValidUntilUtc { get; init; }

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }
}

public sealed class LicenseDeactivationResult
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    public string? LicenseKind { get; init; }

    public string? CanonicalLicenseKey { get; init; }
}

/// <summary>Resolved license row for a unified or mapped REGK key.</summary>
public sealed class LicenseInfo
{
    public string LicenseKey { get; init; } = string.Empty;

    public string CanonicalLicenseKey { get; init; } = string.Empty;

    public string LicenseKind { get; init; } = LicenseKeyKinds.Tenant;

    public string? Slug { get; init; }

    public bool Exists { get; init; }

    public bool IsValid { get; init; }

    public bool IsExpired { get; init; }

    public bool IsRevoked { get; init; }

    public DateTime? ValidUntilUtc { get; init; }

    public Guid? TenantId { get; init; }

    public string? TenantSlug { get; init; }

    public string? CustomerName { get; init; }

    public string? SourceTable { get; init; }

    public Guid? SourceId { get; init; }

    public string? Status { get; init; }
}

/// <summary>One layer of the combined license snapshot (system host or mandant row).</summary>
public sealed class UnifiedLicenseLayerStatusDto
{
    public DateTime? ValidUntil { get; init; }

    /// <summary><c>active</c>, <c>grace</c>, or <c>expired</c>.</summary>
    public string Status { get; init; } = "expired";

    public bool IsActive { get; init; }
}

/// <summary>
/// Combined deployment + mandant license snapshot used by status API, FA, and middleware.
/// <see cref="IsSystemLicense"/> / <see cref="IsTenantLicense"/> mean that layer is currently operational
/// (paid/trial for system; CanAccess and not locked for tenant, including grace).
/// </summary>
public sealed class UnifiedLicenseStatusDto
{
    public bool IsActive { get; set; }

    /// <summary>Primary kind for the request: <c>system</c> or <c>tenant</c>.</summary>
    public string LicenseType { get; set; } = LicenseKeyKinds.System;

    public string Slug { get; set; } = string.Empty;

    public DateTime? ValidUntil { get; set; }

    public bool IsSystemLicense { get; set; }

    public bool IsTenantLicense { get; set; }

    public bool AnyLicenseActive => IsSystemLicense || IsTenantLicense;

    public bool AllLicensesActive => IsSystemLicense && IsTenantLicense;

    public UnifiedLicenseLayerStatusDto SystemLicense { get; set; } = new();

    public UnifiedLicenseLayerStatusDto TenantLicense { get; set; } = new();

    /// <summary>Coarse combined label: <c>active</c>, <c>grace</c>, or <c>expired</c>.</summary>
    public string Status { get; set; } = "expired";

    [JsonIgnore]
    public LicenseStatusResponse? DeploymentSnapshot { get; set; }

    [JsonIgnore]
    public LicenseStatusInfo? MandantSnapshot { get; set; }
}

public sealed record UnifiedLicenseActivationContext(
    Guid? TenantId,
    Guid? ActorUserId,
    ActivateLicenseRequest? DeploymentRequest = null,
    LicenseActivationClientInfo? ClientInfo = null);

public sealed record UnifiedLicenseDeactivationContext(
    Guid? ActorUserId,
    string? Reason = null);

/// <summary>Body for <c>POST /api/license/validate</c> (key only; no activation).</summary>
public sealed class LicenseKeyLookupRequest
{
    [Required]
    public string LicenseKey { get; set; } = string.Empty;
}

public static class UnifiedLicenseRoutes
{
    public const string Activate = "/api/license/activate";
    public const string Validate = "/api/license/validate";
    public const string Info = "/api/license/info";
}
