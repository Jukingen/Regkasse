using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.AdminCashRegisters;

/// <summary>Tenant-scoped cash register inventory admin operations (admin / back-office).</summary>
public interface ICashRegisterManagementService
{
    /// <summary>
    /// Lists cash registers for the resolved tenant scope. SuperAdmin without <paramref name="tenantIdFilter"/>
    /// sees all tenants; with a filter, only that mandant. Other roles always see the effective tenant only.
    /// </summary>
    Task<PagedResult<CashRegisterDto>> ListAsync(
        Guid? tenantIdFilter,
        bool actorIsSuperAdmin,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single register when <paramref name="cashRegisterId"/> is set on the list endpoint;
    /// applies the same tenant authorization rules as <see cref="ListAsync"/>.
    /// </summary>
    Task<CashRegisterDto?> GetByIdAsync(
        Guid cashRegisterId,
        Guid? tenantIdFilter,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Count of registers for <paramref name="tenantId"/> excluding <see cref="RegisterStatus.Decommissioned"/>.
    /// SuperAdmin may query any tenant; other roles only their effective tenant.
    /// </summary>
    Task<int> GetActiveCountForTenantAsync(
        Guid tenantId,
        bool actorIsSuperAdmin,
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
