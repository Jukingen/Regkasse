namespace KasseAPI_Final.Services.AdminTenants;

public interface ITenantUserService
{
    Task<IReadOnlyList<TenantUserDto>?> ListAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<(TenantUserDto? Result, string? Error)> AddAsync(
        Guid tenantId,
        AddAdminTenantUserRequest request,
        CancellationToken cancellationToken = default);

    Task<(TenantUserInviteResultDto? Result, string? Error)> InviteAsync(
        Guid tenantId,
        InviteTenantUserRequest request,
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
        ResetTenantUserPasswordRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<(TenantUserDto? Result, string? Error)> UpdateRoleAsync(
        Guid tenantId,
        string userId,
        UpdateTenantUserRoleRequest request,
        CancellationToken cancellationToken = default);
}
