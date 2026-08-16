using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.License;

/// <summary>
/// Single key-based entry point for server (deployment / <c>issued_licenses</c>) and
/// tenant (billing / <c>license_sales</c>) licenses in unified REGK format.
/// Inner JWT, machine-binding, extend, and snapshot logic stay on
/// <see cref="ILicenseService"/> and billing <c>ITenantLicenseService</c>.
/// </summary>
public interface IUnifiedLicenseService
{
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
