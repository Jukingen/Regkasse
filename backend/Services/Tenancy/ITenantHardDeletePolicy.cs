using KasseAPI_Final.Services.AdminTenants;

namespace KasseAPI_Final.Services.Tenancy;

public interface ITenantHardDeletePolicy
{
    bool IsProduction();

    bool HasFiscalFootprint(TenantDeleteDependencyCountsDto counts);

    IReadOnlyList<TenantDeleteDependencyBlockerDto> GetBlockers(
        TenantDeleteDependencyCountsDto counts,
        bool isProduction);

    (bool CanDelete, string? FailureCode, string? FailureMessage) Validate(
        TenantDeleteDependencyCountsDto counts,
        bool isProduction,
        bool forceDelete);
}
