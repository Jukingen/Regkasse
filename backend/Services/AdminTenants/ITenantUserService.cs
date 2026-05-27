using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services.AdminTenants;

public interface ITenantUserService
{
    Task<IReadOnlyList<TenantUserDto>?> ListAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<(TenantUserDto? Result, string? Error)> AssignExistingAsync(
        Guid tenantId,
        AddAdminTenantUserRequest request,
        CancellationToken cancellationToken = default);

    Task<(CreateTenantUserResultDto? Result, string? Error)> CreateAsync(
        Guid tenantId,
        CreateTenantUserRequest request,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task<(CreateTenantUserResultDto? Result, string? Error)> CreateQuickAsync(
        Guid tenantId,
        CreateQuickTenantUserRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<(TenantUserDto? Result, string? Error)> UpdateAsync(
        Guid tenantId,
        string userId,
        UpdateAdminTenantUserRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> RemoveAsync(
        Guid tenantId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<(TenantUserPasswordResetResultDto? Result, string? Error)> ResetPasswordAsync(
        Guid tenantId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<(TenantUserDto? Result, string? Error)> UpdateRoleAsync(
        Guid tenantId,
        string userId,
        UpdateTenantUserRoleRequest request,
        CancellationToken cancellationToken = default);
}
