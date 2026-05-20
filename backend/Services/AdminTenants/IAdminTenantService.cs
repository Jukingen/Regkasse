namespace KasseAPI_Final.Services.AdminTenants;

public interface IAdminTenantService
{
    Task<IReadOnlyList<AdminTenantListItemDto>> ListAsync(bool includeDeleted, CancellationToken cancellationToken = default);

    Task<AdminTenantDetailDto?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TenantSlugAvailabilityDto> CheckSlugAvailabilityAsync(
        string slug,
        CancellationToken cancellationToken = default);

    Task<(AdminTenantDetailDto? Result, string? Error)> CreateAsync(
        CreateAdminTenantRequest request,
        string? actorUserId,
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

    Task<(TenantImpersonationResponseDto? Result, string? Error)> ImpersonateAsync(
        Guid tenantId,
        string actorUserId,
        CancellationToken cancellationToken = default);
}
