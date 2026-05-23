namespace KasseAPI_Final.Services.AdminTenants;

public interface IAdminTenantLicenseService
{
    Task<TenantLicenseOverviewDto?> GetOverviewAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<(TenantLicenseOverviewDto? Result, string? Error)> ActivateTrialAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<(TenantLicenseOverviewDto? Result, string? Error)> ExtendAsync(
        Guid tenantId,
        ExtendTenantLicenseRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<(TenantLicenseOverviewDto? Result, string? Error)> SetTierAsync(
        Guid tenantId,
        SetTenantLicenseTierRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<(TenantLicenseConsistencyDto? Result, string? Error)> CheckDeploymentConsistencyAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<(TenantLicenseIssueDeploymentResultDto? Result, string? Error)> IssueDeploymentLicenseAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default);
}
