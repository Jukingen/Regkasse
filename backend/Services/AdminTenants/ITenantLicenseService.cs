namespace KasseAPI_Final.Services.AdminTenants;

/// <summary>Tenant license key validation and read-only preview (no tenant mutations).</summary>
public interface ITenantLicenseService
{
    /// <summary>
    /// Validates a license key and returns preview details.
    /// Managers: <c>license_sales</c> billing keys. Super Admin: <c>issued_licenses</c> deployment keys.
    /// Read-only: does not modify tenant license state.
    /// </summary>
    Task<(LicensePreviewResult? Result, string? Error)> PreviewLicenseAsync(
        Guid tenantId,
        string licenseKey,
        bool isSuperAdmin,
        CancellationToken cancellationToken = default);

    /// <summary>Shared issued-license resolution for Super Admin extend flows.</summary>
    Task<IssuedLicenseResolveResult> ResolveIssuedLicenseForKeyAsync(
        Guid tenantId,
        string licenseKey,
        bool isSuperAdmin,
        CancellationToken cancellationToken = default);

    /// <summary>Manager mandant activation: resolve billing key from <c>license_sales</c>.</summary>
    Task<BillingLicenseSaleResolveResult> ResolveBillingLicenseSaleForKeyAsync(
        Guid tenantId,
        string licenseKey,
        CancellationToken cancellationToken = default);
}
