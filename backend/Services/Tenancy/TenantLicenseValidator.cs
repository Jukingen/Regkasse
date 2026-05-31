using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.Tenancy;

public enum TenantLicenseStatus
{
    Active,
    GraceWrite,
    GraceReadOnly,
    Lockdown,
    NoLicense,
}

public readonly record struct TenantLicensePermissions(
    bool CanWrite,
    bool CanManageUsers,
    bool CanAccess);

public sealed class TenantLicenseValidator
{
    public static int GraceDaysWrite => LicenseGracePeriodConfig.GracePeriodDays;

    /// <summary>First day (inclusive) after grace when lockdown applies.</summary>
    public static int LockdownStartsAfterDaysExpired =>
        LicenseGracePeriodConfig.GracePeriodDays + LicenseGracePeriodConfig.BlockAfterGraceDays;

    public TenantLicenseStatus GetStatus(DateTime? validUntilUtc, DateTime? nowUtc = null)
    {
        if (!validUntilUtc.HasValue)
            return TenantLicenseStatus.NoLicense;

        var now = nowUtc ?? DateTime.UtcNow;
        var until = DateTime.SpecifyKind(validUntilUtc.Value, DateTimeKind.Utc);
        var daysExpired = (now - until).Days;

        if (daysExpired <= 0)
            return TenantLicenseStatus.Active;

        if (daysExpired <= GraceDaysWrite)
            return TenantLicenseStatus.GraceWrite;

        return TenantLicenseStatus.Lockdown;
    }

    public TenantLicensePermissions GetPermissions(
        DateTime? validUntilUtc,
        bool isSuperAdmin,
        DateTime? nowUtc = null)
    {
        if (isSuperAdmin)
            return new TenantLicensePermissions(CanWrite: true, CanManageUsers: true, CanAccess: true);

        return GetStatus(validUntilUtc, nowUtc) switch
        {
            TenantLicenseStatus.Active => new TenantLicensePermissions(true, true, true),
            TenantLicenseStatus.GraceWrite => new TenantLicensePermissions(true, true, true),
            TenantLicenseStatus.GraceReadOnly => new TenantLicensePermissions(false, true, true),
            TenantLicenseStatus.Lockdown => new TenantLicensePermissions(false, false, false),
            TenantLicenseStatus.NoLicense => new TenantLicensePermissions(false, false, false),
            _ => new TenantLicensePermissions(false, false, false),
        };
    }
}
