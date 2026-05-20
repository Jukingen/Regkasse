using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed record AdminTenantListItemDto(
    Guid Id,
    string Name,
    string Slug,
    string? Email,
    string? Phone,
    string Status,
    bool IsActive,
    string? LicenseKey,
    DateTime? LicenseValidUntilUtc,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? OwnerAdminEmail = null,
    bool IsDemoPreset = false);

public sealed record AdminTenantDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string? Email,
    string? Phone,
    string? Address,
    string Status,
    bool IsActive,
    string? LicenseKey,
    DateTime? LicenseValidUntilUtc,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DeletedAtUtc,
    TenantProvisioningDto? Provisioning = null);

public sealed class CreateAdminTenantRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    public string? Address { get; set; }

    [MaxLength(100)]
    public string? LicenseKey { get; set; }

    public DateTime? LicenseValidUntilUtc { get; set; }

    /// <summary>Optional login email for the provisioned tenant admin (default: admin@{slug}.regkasse.at).</summary>
    [MaxLength(200)]
    [EmailAddress]
    public string? AdminEmail { get; set; }

    /// <summary>Optional initial password; auto-generated when omitted (returned once in response).</summary>
    [MaxLength(100)]
    public string? AdminPassword { get; set; }

    /// <summary>When true and no license end date is set, grants a 30-day trial on the tenant row.</summary>
    public bool GrantTrialLicense { get; set; } = true;
}

public sealed class UpdateAdminTenantRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    public string? Address { get; set; }

    [MaxLength(20)]
    public string? Status { get; set; }

    [MaxLength(100)]
    public string? LicenseKey { get; set; }

    public DateTime? LicenseValidUntilUtc { get; set; }

    public bool? IsActive { get; set; }
}

public sealed record TenantSlugAvailabilityDto(
    string NormalizedSlug,
    bool IsValid,
    bool Available);

public sealed record TenantImpersonationResponseDto(
    string Token,
    int ExpiresIn,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAtUtc,
    Guid TenantId,
    string TenantSlug,
    string? TenantDisplayName,
    bool Impersonation);
