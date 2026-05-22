using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Tenancy;

/// <summary>
/// Maps <see cref="Tenant"/> mandant license columns to deployment-style <see cref="LicenseStatusResponse"/>
/// (same day math as <see cref="AdminTenants.AdminTenantLicenseService"/> and FA <c>resolveTenantLicenseLabel</c>).
/// </summary>
public static class TenantLicenseStatusMapper
{
    public static (int? DaysRemaining, string Kind) ComputeKindAndDays(
        DateTime? licenseValidUntilUtc,
        string? licenseKey,
        DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        if (!licenseValidUntilUtc.HasValue)
            return (null, "none");

        var until = DateTime.SpecifyKind(licenseValidUntilUtc.Value, DateTimeKind.Utc);
        var days = (int)Math.Ceiling((until - now).TotalDays);
        if (days < 0)
            return (days, "expired");

        return string.IsNullOrWhiteSpace(licenseKey) ? (days, "trial") : (days, "active");
    }

    /// <summary>
    /// Builds a POS/admin license snapshot from the tenant row when <paramref name="tenant"/> has <see cref="Tenant.LicenseValidUntilUtc"/>.
    /// </summary>
    public static LicenseStatusResponse? TryMapToLicenseStatus(Tenant tenant, string machineHash, DateTime? nowUtc = null)
    {
        if (!tenant.LicenseValidUntilUtc.HasValue)
            return null;

        var now = nowUtc ?? DateTime.UtcNow;
        var until = DateTime.SpecifyKind(tenant.LicenseValidUntilUtc.Value, DateTimeKind.Utc);
        var (daysRaw, kind) = ComputeKindAndDays(until, tenant.LicenseKey, now);
        var days = daysRaw ?? 0;

        if (kind == "expired")
        {
            return new LicenseStatusResponse(
                IsValid: false,
                IsTrial: false,
                IsExpired: true,
                DaysRemaining: 0,
                ExpiryDate: until,
                MachineHash: machineHash,
                EnabledFeatures: Array.Empty<string>());
        }

        if (kind == "trial")
        {
            return new LicenseStatusResponse(
                IsValid: false,
                IsTrial: true,
                IsExpired: false,
                DaysRemaining: Math.Max(0, days),
                ExpiryDate: until,
                MachineHash: machineHash,
                EnabledFeatures: LicenseFeatureIds.All);
        }

        return new LicenseStatusResponse(
            IsValid: true,
            IsTrial: false,
            IsExpired: false,
            DaysRemaining: Math.Max(0, days),
            ExpiryDate: until,
            MachineHash: machineHash,
            EnabledFeatures: LicenseFeatureIds.All);
    }
}
