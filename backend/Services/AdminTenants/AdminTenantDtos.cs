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
    DateTime? UpdatedAt);

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
    DateTime? DeletedAtUtc);

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

public sealed record TenantImpersonationResponseDto(
    string Token,
    int ExpiresIn,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAtUtc,
    Guid TenantId,
    string TenantSlug,
    string? TenantDisplayName,
    bool Impersonation);
