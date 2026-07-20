using KasseAPI_Final.Models;
using KasseAPI_Final.Services.License;

namespace KasseAPI_Final.Services;

/// <summary>Maps deployment and mandant license snapshots to <see cref="LicensePublicStatusDto"/>.</summary>
public static class LicensePublicStatusMapper
{
    public static LicensePublicStatusDto MapDeploymentStatus(LicenseStatusResponse s)
    {
        var paid = s.IsValid && !s.IsTrial;
        var trialActive = s.IsTrial && !s.IsExpired;
        var licenseType = paid ? "Licensed" : trialActive ? "Trial" : "Expired";
        var isValidPublic = paid || trialActive;

        IReadOnlyList<string> features;
        if (!isValidPublic)
            features = Array.Empty<string>();
        else
            features = s.EnabledFeatures is { Count: > 0 } ? s.EnabledFeatures : LicenseFeatureIds.All;

        DateTime? validUntil = s.ExpiryDate.HasValue
            ? DateTime.SpecifyKind(s.ExpiryDate.Value, DateTimeKind.Utc)
            : null;

        var mode = trialActive ? "Trial" : "Production";

        return new LicensePublicStatusDto
        {
            LicenseType = licenseType,
            ValidUntil = validUntil,
            DaysRemaining = s.DaysRemaining,
            Features = features,
            IsExpired = s.IsExpired,
            IsValid = isValidPublic,
            Mode = mode,
            IsDevelopmentBypass = s.IsDevelopmentBypass,
        };
    }

    public static LicensePublicStatusDto ApplyMandantOverlay(
        LicensePublicStatusDto deployment,
        LicenseStatusInfo mandant,
        string? language = null)
    {
        var statusMessage = !string.IsNullOrWhiteSpace(mandant.StatusMessageKey)
            ? LicenseStatusMessages.Format(
                mandant.StatusMessageKey,
                language,
                daysRemaining: Math.Max(0, mandant.DaysRemaining),
                daysOverdue: mandant.DaysOverdue,
                gracePeriodRemaining: mandant.GracePeriodRemaining,
                lockDateUtc: mandant.LockDate)
            : mandant.StatusMessage;

        return new LicensePublicStatusDto
        {
            LicenseType = deployment.LicenseType,
            ValidUntil = mandant.ValidUntil ?? deployment.ValidUntil,
            DaysRemaining = mandant.DaysRemaining,
            Features = deployment.Features,
            IsExpired = mandant.IsExpired || (!mandant.CanAccess && mandant.RequiresRenewal),
            IsValid = mandant.CanAccess,
            Mode = deployment.Mode,
            IsDevelopmentBypass = deployment.IsDevelopmentBypass,
            CanAccess = mandant.CanAccess,
            CanTransact = mandant.CanTransact,
            StatusMessage = statusMessage,
            StatusMessageKey = string.IsNullOrWhiteSpace(mandant.StatusMessageKey)
                ? null
                : mandant.StatusMessageKey,
            IsInGracePeriod = mandant.IsInGracePeriod,
            IsLocked = mandant.IsLocked,
            DaysOverdue = mandant.DaysOverdue,
            GracePeriodRemaining = mandant.GracePeriodRemaining,
            LockDate = mandant.LockDate,
            Restrictions = mandant.Restrictions,
            RequiresRenewal = mandant.RequiresRenewal,
        };
    }
}
