namespace KasseAPI_Final.Services.AdminTenants;

public interface IAdminTenantService
{
    Task<IReadOnlyList<AdminTenantListItemDto>> ListAsync(bool includeDeleted, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dev/header tenant switcher: all tenants for SuperAdmin; membership-scoped for other users.
    /// </summary>
    Task<IReadOnlyList<AdminTenantListItemDto>> ListForSwitcherAsync(
        string? actorUserId,
        bool actorIsSuperAdmin,
        bool includeDeleted,
        CancellationToken cancellationToken = default);

    Task<AdminTenantDetailDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminTenantCashRegisterDto>?> ListCashRegistersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TenantSlugAvailabilityDto> CheckSlugAvailabilityAsync(
        string slug,
        CancellationToken cancellationToken = default);

    Task<(AdminTenantDetailDto? Result, string? Error)> CreateAsync(
        CreateAdminTenantRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<(AdminTenantDetailDto? Result, TenantOnboardingFailureDto? Failure)> CreateWithFailureDetailAsync(
        CreateAdminTenantRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSlugSuggestionsAsync(
        string? companyName,
        string? preferredSlug,
        int maxCount = 5,
        CancellationToken cancellationToken = default);

    Task<(AdminTenantDetailDto? Result, string? Error)> UpdateAsync(
        Guid tenantId,
        UpdateAdminTenantRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> SoftDeleteAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> HardDeleteAsync(
        Guid tenantId,
        HardDeleteAdminTenantRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<(TenantImpersonationResponseDto? Result, string? Error)> ImpersonateAsync(
        Guid tenantId,
        string actorUserId,
        CancellationToken cancellationToken = default);
}
