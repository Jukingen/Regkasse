using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.Tenancy;

public enum TenantLicenseStatus
{
    Active,
    GraceWrite,
    /// <summary>Reserved when <see cref="LicenseGracePeriodConfig.BlockAfterGraceDays"/> &gt; 0.</summary>
    GraceReadOnly,
    /// <summary>Locked phase: days overdue after grace through <see cref="LicenseGracePeriodConfig.ArchiveAfterDays"/>.</summary>
    Lockdown,
    /// <summary>Archived phase: days overdue &gt; <see cref="LicenseGracePeriodConfig.ArchiveAfterDays"/>.</summary>
    Archived,
    NoLicense,
}

public readonly record struct TenantLicensePermissions(
    bool CanWrite,
    bool CanManageUsers,
    bool CanAccess);

public sealed class TenantLicenseValidator
{
    public static int GraceDaysWrite => LicenseGracePeriodConfig.GracePeriodDays;

    /// <summary>First day (inclusive) after grace when lockdown (Locked) applies.</summary>
    public static int LockdownStartsAfterDaysExpired =>
        LicenseGracePeriodConfig.GracePeriodDays + LicenseGracePeriodConfig.BlockAfterGraceDays;

    /// <summary>Inclusive last day of Locked; the next day is Archived.</summary>
    public static int ArchiveStartsAfterDaysExpired => LicenseGracePeriodConfig.ArchiveAfterDays;

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

        if (daysExpired <= ArchiveStartsAfterDaysExpired)
            return TenantLicenseStatus.Lockdown;

        return TenantLicenseStatus.Archived;
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
            // Locked / Archived: POS blocked via LicenseMiddleware + CanAccess=false;
            // FA read-only is allowed by middleware despite CanAccess=false (GET + reports).
            TenantLicenseStatus.Lockdown => new TenantLicensePermissions(false, false, false),
            TenantLicenseStatus.Archived => new TenantLicensePermissions(false, false, false),
            TenantLicenseStatus.NoLicense => new TenantLicensePermissions(false, false, false),
            _ => new TenantLicensePermissions(false, false, false),
        };
    }
}
