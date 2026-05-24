using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.AdminCashRegisters;

/// <summary>Tenant-scoped cash register inventory admin operations (admin / back-office).</summary>
public interface ICashRegisterManagementService
{
    /// <summary>
    /// Lists cash registers for the resolved tenant scope. SuperAdmin may pass <paramref name="tenantIdFilter"/>;
    /// other roles always see the effective tenant only.
    /// </summary>
    Task<PagedResult<CashRegisterDto>> ListAsync(
        Guid? tenantIdFilter,
        bool actorIsSuperAdmin,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a closed cash register row. <paramref name="request"/>.TenantId is honored only when
    /// <paramref name="actorIsSuperAdmin"/> is true; otherwise the effective tenant from <see cref="Tenancy.ISettingsTenantResolver"/> is used.
    /// </summary>
    Task<CashRegister> CreateAsync(
        CreateCashRegisterRequest request,
        string actorUserId,
        string actorRole,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    /// <summary>Updates register number and location for a tenant-scoped row (not decommissioned).</summary>
    Task<CashRegisterDto> UpdateAsync(
        Guid id,
        UpdateCashRegisterRequest request,
        string actorUserId,
        string actorRole,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);
}
