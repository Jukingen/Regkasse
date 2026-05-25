using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public enum DeploymentLicenseStatus
{
    Active,
    GraceWrite,
    GraceReadOnly,
    Lockdown,
    NoLicense,
}

public readonly record struct DeploymentLicensePermissions(
    bool CanWrite,
    bool CanAccess);

public sealed class DeploymentLicenseValidator
{
    public const int GraceDaysWrite = 15;
    public const int LockdownDays = 60;

    public DeploymentLicenseStatus GetStatus(LicenseStatusResponse snapshot, DateTime? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.IsValid || (snapshot.IsTrial && !snapshot.IsExpired))
            return DeploymentLicenseStatus.Active;

        if (!snapshot.ExpiryDate.HasValue)
            return DeploymentLicenseStatus.NoLicense;

        var now = nowUtc ?? DateTime.UtcNow;
        var expiry = DateTime.SpecifyKind(snapshot.ExpiryDate.Value, DateTimeKind.Utc);
        var daysExpired = (now - expiry).Days;

        if (daysExpired <= GraceDaysWrite)
            return DeploymentLicenseStatus.GraceWrite;

        if (daysExpired <= LockdownDays)
            return DeploymentLicenseStatus.GraceReadOnly;

        return DeploymentLicenseStatus.Lockdown;
    }

    public DeploymentLicensePermissions GetPermissions(LicenseStatusResponse snapshot, DateTime? nowUtc = null)
    {
        return GetStatus(snapshot, nowUtc) switch
        {
            DeploymentLicenseStatus.Active => new DeploymentLicensePermissions(true, true),
            DeploymentLicenseStatus.GraceWrite => new DeploymentLicensePermissions(true, true),
            DeploymentLicenseStatus.GraceReadOnly => new DeploymentLicensePermissions(false, true),
            DeploymentLicenseStatus.Lockdown => new DeploymentLicensePermissions(false, false),
            DeploymentLicenseStatus.NoLicense => new DeploymentLicensePermissions(false, false),
            _ => new DeploymentLicensePermissions(false, false),
        };
    }
}
