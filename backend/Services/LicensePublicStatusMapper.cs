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
            Status = isValidPublic ? "active" : "expired",
            AnyActive = isValidPublic,
            AllActive = false,
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
            Status = mandant.IsInGracePeriod ? "grace" : (mandant.CanAccess ? "active" : "expired"),
            AnyActive = mandant.CanAccess || deployment.AnyActive,
            AllActive = mandant.CanAccess && deployment.AnyActive,
        };
    }

    public static LicensePublicStatusDto MapUnified(UnifiedLicenseStatusDto unified, string? language = null)
    {
        var deployment = unified.DeploymentSnapshot
            ?? new LicenseStatusResponse(false, false, true, 0, null, string.Empty);
        var dto = MapDeploymentStatus(deployment);
        if (unified.MandantSnapshot is LicenseStatusInfo mandant)
            dto = ApplyMandantOverlay(dto, mandant, language);

        return CopyWithUnifiedLayers(dto, unified);
    }

    private static LicensePublicStatusDto CopyWithUnifiedLayers(
        LicensePublicStatusDto dto,
        UnifiedLicenseStatusDto unified)
    {
        return new LicensePublicStatusDto
        {
            LicenseType = dto.LicenseType,
            ValidUntil = unified.ValidUntil ?? dto.ValidUntil,
            DaysRemaining = dto.DaysRemaining,
            Features = dto.Features,
            IsExpired = dto.IsExpired,
            IsValid = dto.IsValid,
            Mode = dto.Mode,
            IsDevelopmentBypass = dto.IsDevelopmentBypass,
            CanAccess = dto.CanAccess,
            CanTransact = dto.CanTransact,
            StatusMessage = dto.StatusMessage,
            StatusMessageKey = dto.StatusMessageKey,
            IsInGracePeriod = dto.IsInGracePeriod,
            IsLocked = dto.IsLocked,
            DaysOverdue = dto.DaysOverdue,
            GracePeriodRemaining = dto.GracePeriodRemaining,
            LockDate = dto.LockDate,
            Restrictions = dto.Restrictions,
            RequiresRenewal = dto.RequiresRenewal,
            Status = unified.Status,
            SystemLicense = ToPublicLayer(unified.SystemLicense),
            TenantLicense = ToPublicLayer(unified.TenantLicense),
            AnyActive = unified.AnyLicenseActive,
            AllActive = unified.AllLicensesActive,
        };
    }

    private static LicenseLayerPublicStatusDto ToPublicLayer(UnifiedLicenseLayerStatusDto layer) =>
        new()
        {
            ValidUntil = layer.ValidUntil,
            Status = layer.Status,
            IsActive = layer.IsActive,
        };
}
