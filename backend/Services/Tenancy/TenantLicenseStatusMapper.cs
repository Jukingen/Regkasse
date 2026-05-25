using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Tenancy;

/// <summary>
/// Maps <see cref="Tenant"/> mandant license columns to deployment-style <see cref="LicenseStatusResponse"/>
/// (same day math as <see cref="AdminTenants.AdminTenantLicenseService"/> and FA <c>resolveTenantLicenseLabel</c>).
/// </summary>
public static class TenantLicenseStatusMapper
{
    private static readonly TenantLicenseValidator Validator = new();

    public static (int? DaysRemaining, string Kind) ComputeKindAndDays(
        DateTime? licenseValidUntilUtc,
        string? licenseKey,
        DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        if (!licenseValidUntilUtc.HasValue)
            return (null, "no_license");

        var until = DateTime.SpecifyKind(licenseValidUntilUtc.Value, DateTimeKind.Utc);
        var days = (int)Math.Ceiling((until - now).TotalDays);
        var status = Validator.GetStatus(until, now);

        return status switch
        {
            TenantLicenseStatus.Active => (days, "active"),
            TenantLicenseStatus.GraceWrite => (days, "grace_write"),
            TenantLicenseStatus.GraceReadOnly => (days, "grace_read_only"),
            TenantLicenseStatus.Lockdown => (days, "lockdown"),
            TenantLicenseStatus.NoLicense => (null, "no_license"),
            _ => (days, string.IsNullOrWhiteSpace(licenseKey) ? "active" : "active"),
        };
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
        var (daysRaw, _) = ComputeKindAndDays(until, tenant.LicenseKey, now);
        var days = daysRaw ?? 0;
        var status = Validator.GetStatus(until, now);

        if (status == TenantLicenseStatus.GraceReadOnly
            || status == TenantLicenseStatus.Lockdown
            || status == TenantLicenseStatus.NoLicense)
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

        var isTrialLike = string.IsNullOrWhiteSpace(tenant.LicenseKey);
        return new LicenseStatusResponse(
            IsValid: !isTrialLike,
            IsTrial: isTrialLike,
            IsExpired: false,
            DaysRemaining: Math.Max(0, days),
            ExpiryDate: until,
            MachineHash: machineHash,
            EnabledFeatures: LicenseFeatureIds.All);
    }
}
