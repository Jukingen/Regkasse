using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models;

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
    int? LicenseDaysRemaining = null,
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
    string? OwnerAdminEmail = null,
    int ActiveUserCount = 0,
    int CashRegisterCount = 0,
    DateTime? LastActivityAtUtc = null,
    TenantProvisioningDto? Provisioning = null,
    string OperationMode = TenantOperationModes.Active,
    string? MaintenanceMessage = null,
    DateTime? MaintenanceStartedAt = null,
    DateTime? MaintenanceEndsAt = null);

public sealed record AdminTenantCashRegisterDto(
    Guid Id,
    string RegisterNumber,
    string Location,
    string Status,
    bool IsActive,
    DateTime? LastUsedAtUtc);

public sealed record TenantDecommissionChecksDto(
    bool HasOpenPayments,
    bool HasOpenShifts,
    int ActiveRegistersCount,
    int ReadyRegistersCount,
    int BlockedRegistersCount,
    bool CanDecommission);

public sealed record TenantDeleteDependencyCountsDto(
    int Users,
    int Memberships,
    int CashRegisters,
    int Payments,
    int Receipts,
    int Vouchers,
    int VoucherLedgerEntries,
    int DailyClosings,
    int Products,
    int Categories,
    int AuditLogs,
    int FinanzOnlineSubmissions);

public sealed record TenantDeleteDependencyBlockerDto(
    string Code,
    int Count,
    string Severity,
    string Message);

public sealed record TenantDeleteDependenciesDto(
    Guid TenantId,
    string TenantSlug,
    string TenantStatus,
    bool CanHardDelete,
    bool HasDependencies,
    bool HasFiscalFootprint,
    string? FailureCode,
    string? FailureMessage,
    TenantDeleteDependencyCountsDto Dependencies,
    IReadOnlyList<TenantDeleteDependencyBlockerDto> BlockingDependencies,
    IReadOnlyList<string> NextSteps);

public sealed record TenantPermanentDeleteResult(
    bool Success,
    string? Message = null,
    string? Code = null,
    TenantDeleteDependenciesDto? Dependencies = null);

public sealed record TenantPermanentDeleteErrorResponse(
    string Message,
    string Code,
    TenantDeleteDependenciesDto? Dependencies = null);

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

    /// <summary>When true, imports the full demo menu (Salate, Pizzas, Pasta, …) instead of three generic demo products.</summary>
    public bool ImportDemoMenu { get; set; }

    /// <summary>Optional cash register number for provisioning (default: KASSE-001).</summary>
    [MaxLength(20)]
    public string? CashRegisterNumber { get; set; }

    /// <summary>Industry template id (restaurant|retail|hotel) or null/none.</summary>
    [MaxLength(32)]
    public string? IndustryTemplateId { get; set; }

    /// <summary>When true and an industry template is set, seed deactivated starter users for template slots.</summary>
    public bool SeedIndustryStarterUsers { get; set; } = true;
}

public sealed class HardDeleteAdminTenantRequest
{
    /// <summary>Must match tenant slug exactly (safety confirmation).</summary>
    [Required]
    [MaxLength(64)]
    public string ConfirmSlug { get; set; } = string.Empty;
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

public sealed class UpdateTenantOperationModeRequest
{
    [Required]
    [MaxLength(20)]
    public string OperationMode { get; set; } = TenantOperationModes.Active;

    [MaxLength(2000)]
    public string? MaintenanceMessage { get; set; }

    public DateTime? MaintenanceStartedAt { get; set; }

    public DateTime? MaintenanceEndsAt { get; set; }
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
