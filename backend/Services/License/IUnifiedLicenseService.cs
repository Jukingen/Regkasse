using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.License;

/// <summary>
/// Single key-based entry point for server (deployment / <c>issued_licenses</c>) and
/// tenant (billing / <c>license_sales</c>) licenses in unified REGK format.
/// Inner JWT, machine-binding, extend, and snapshot logic stay on the
/// deployment adapter <see cref="ILicenseService"/> and billing <c>ITenantLicenseService</c>.
/// Consumers (controllers, middleware, FA) should prefer <see cref="IUnifiedLicenseService"/>.
/// </summary>
public interface IUnifiedLicenseService
{
    /// <summary>
    /// Combined host + mandant operational snapshot. When <paramref name="tenantId"/> is null,
    /// ambient tenant from <see cref="KasseAPI_Final.Tenancy.ICurrentTenantAccessor"/> is used.
    /// </summary>
    Task<UnifiedLicenseStatusDto> GetUnifiedStatusAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<LicenseKeyValidationResult> ValidateLicenseAsync(
        string licenseKey,
        CancellationToken cancellationToken = default);

    Task<LicenseActivationResult> ActivateLicenseAsync(
        string licenseKey,
        CancellationToken cancellationToken = default);

    Task<LicenseActivationResult> ActivateLicenseAsync(
        string licenseKey,
        UnifiedLicenseActivationContext context,
        CancellationToken cancellationToken = default);

    Task<LicenseDeactivationResult> DeactivateLicenseAsync(
        string licenseKey,
        CancellationToken cancellationToken = default);

    Task<LicenseDeactivationResult> DeactivateLicenseAsync(
        string licenseKey,
        UnifiedLicenseDeactivationContext? context,
        CancellationToken cancellationToken = default);

    Task<bool> IsLicenseValidAsync(
        string licenseKey,
        CancellationToken cancellationToken = default);

    Task<LicenseInfo> GetLicenseInfoAsync(
        string licenseKey,
        CancellationToken cancellationToken = default);
}
