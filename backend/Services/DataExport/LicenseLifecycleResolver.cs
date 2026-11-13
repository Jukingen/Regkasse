using KasseAPI_Final.Models;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Services.Tenancy;

namespace KasseAPI_Final.Services.DataExport;

/// <summary>
/// Resolves the customer-facing license lifecycle including export/deletion overlays.
/// </summary>
public interface ILicenseLifecycleResolver
{
    LicenseLifecycleState Resolve(
        DateTime? licenseValidUntilUtc,
        DateTime? customerDataPurgedAtUtc,
        bool hasPendingDeletionRequest,
        DateTime? nowUtc = null);

    LicenseLifecycleState Resolve(Tenant tenant, bool hasPendingDeletionRequest, DateTime? nowUtc = null);
}

public sealed class LicenseLifecycleResolver : ILicenseLifecycleResolver
{
    private static readonly TenantLicenseValidator Validator = new();

    public LicenseLifecycleState Resolve(Tenant tenant, bool hasPendingDeletionRequest, DateTime? nowUtc = null) =>
        Resolve(tenant.LicenseValidUntilUtc, tenant.CustomerDataPurgedAtUtc, hasPendingDeletionRequest, nowUtc);

    public LicenseLifecycleState Resolve(
        DateTime? licenseValidUntilUtc,
        DateTime? customerDataPurgedAtUtc,
        bool hasPendingDeletionRequest,
        DateTime? nowUtc = null)
    {
        if (customerDataPurgedAtUtc.HasValue)
            return LicenseLifecycleState.Deleted;

        if (hasPendingDeletionRequest)
            return LicenseLifecycleState.ExportRequest;

        return Validator.GetStatus(licenseValidUntilUtc, nowUtc) switch
        {
            TenantLicenseStatus.Active => LicenseLifecycleState.Active,
            TenantLicenseStatus.GraceWrite => LicenseLifecycleState.Grace,
            TenantLicenseStatus.GraceReadOnly => LicenseLifecycleState.Grace,
            TenantLicenseStatus.Lockdown => LicenseLifecycleState.Locked,
            TenantLicenseStatus.Archived => LicenseLifecycleState.Archived,
            TenantLicenseStatus.NoLicense => LicenseLifecycleState.Locked,
            _ => LicenseLifecycleState.Locked,
        };
    }
}
