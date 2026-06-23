namespace KasseAPI_Final.Services.Billing;

public interface ITenantLicenseService
{
    Task<TenantLicenseStatus> GetCurrentStatusAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<ActivationResult> ActivateLicenseAsync(
        Guid tenantId,
        string licenseKey,
        Guid activatedByUserId,
        CancellationToken ct = default);

    Task<bool> IsLicenseValidAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<TenantLicenseInfo> GetLicenseInfoAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<List<LicenseSaleResponse>> GetLicenseHistoryAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<ExtendResult> ExtendLicenseAsync(
        Guid tenantId,
        string licenseKey,
        Guid extendedByUserId,
        CancellationToken ct = default);

    Task<List<ExpiringLicenseInfo>> GetExpiringLicensesAsync(
        int daysThreshold = 30,
        CancellationToken ct = default);
}
