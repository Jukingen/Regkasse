namespace KasseAPI_Final.Services.LicenseTest;

/// <summary>Development-only helpers to manipulate tenant and deployment license expiry for QA.</summary>
public interface ILicenseTestService
{
    Task<LicenseTestSnapshotDto> GetSnapshotAsync(Guid? tenantId, CancellationToken cancellationToken = default);

    Task<LicenseTestSnapshotDto> SetTenantExpiryAsync(
        LicenseTestTenantRequest request,
        CancellationToken cancellationToken = default);

    Task<LicenseTestSnapshotDto> SetDeploymentExpiryAsync(
        LicenseTestSetExpiryRequest request,
        CancellationToken cancellationToken = default);

    Task<LicenseTestSnapshotDto> ApplyScenarioAsync(
        LicenseTestScenarioRequest request,
        CancellationToken cancellationToken = default);

    Task<LicenseTestSnapshotDto> UpdateAsync(
        LicenseTestRequest request,
        CancellationToken cancellationToken = default);
}
