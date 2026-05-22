namespace KasseAPI_Final.Services.Tenancy;

/// <summary>
/// Keeps <see cref="Models.Tenant.LicenseValidUntilUtc"/> aligned with the latest active <c>issued_licenses</c> row.
/// </summary>
public interface ILicenseSyncService
{
    /// <summary>
    /// Updates one tenant from the newest active issued row for its <see cref="Models.Tenant.LicenseKey"/> (REGK keys only).
    /// Trial-only tenants (no key) are left unchanged.
    /// </summary>
    Task SyncTenantLicenseExpiryAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates every tenant that references <paramref name="licenseKey"/> or matches the issued row customer name.
    /// </summary>
    Task SyncTenantsForLicenseKeyAsync(string licenseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// After renew/upgrade/transfer when the REGK display key changes, updates tenants still holding <paramref name="previousLicenseKey"/>.
    /// </summary>
    Task SyncTenantsForLicenseKeyReplacementAsync(
        string? previousLicenseKey,
        string newLicenseKey,
        CancellationToken cancellationToken = default);
}
