using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>
/// Central switch for disabling license/grace/offline enforcement in Development and Demo (soft TSE) hosts.
/// </summary>
public static class LicenseEnforcementPolicy
{
    /// <summary>Effectively unlimited offline queue cap for Development/Demo hosts.</summary>
    public const int MaxOfflineTransactionsUnlimited = 999_999;

    /// <summary>RKSV default cap per cash register when not configured.</summary>
    public const int MaxOfflineTransactionsProductionDefault = 50;

    /// <summary>
    /// When true, license expiry, grace-period write blocks, and offline queue caps are not enforced.
    /// </summary>
    public static bool ShouldDisableEnforcement(
        IHostEnvironment? environment,
        TseOptions? tseOptions = null,
        IDevelopmentModeService? developmentMode = null,
        LicenseOptions? licenseOptions = null)
    {
        if (OpenApiExportMode.IsEnabled)
            return true;

        if (licenseOptions is { Enabled: false })
            return true;

        if (environment?.IsDevelopment() == true)
            return true;

        if (tseOptions?.UseSoftTseWhenNoDevice == true)
            return true;

        if (developmentMode?.ShouldBypassLicense() == true)
            return true;

        return false;
    }

    /// <summary>
    /// When true, skip <c>tenant_limits.max_offline_transactions</c> (Development / Demo / license-disabled).
    /// </summary>
    public static bool ShouldSkipOfflineQueueCaps(
        IHostEnvironment? environment,
        TseOptions? tseOptions = null,
        IDevelopmentModeService? developmentMode = null,
        LicenseOptions? licenseOptions = null) =>
        ShouldDisableEnforcement(environment, tseOptions, developmentMode, licenseOptions);

    /// <summary>
    /// Obsolete: queue size lives on <c>tenant_limits</c>. Returns unlimited when enforcement is skipped,
    /// otherwise the production default (50) for leftover monitoring callers.
    /// </summary>
    [Obsolete("Use ITenantLimitService / TenantLimitGuard.EnsureCanQueueOfflineTransactionAsync.")]
    public static int GetMaxOfflineTransactionsPerCashRegister(
        IHostEnvironment? environment,
        TseOptions? tseOptions = null,
        IDevelopmentModeService? developmentMode = null,
        LicenseOptions? licenseOptions = null)
    {
        if (ShouldSkipOfflineQueueCaps(environment, tseOptions, developmentMode, licenseOptions))
            return MaxOfflineTransactionsUnlimited;

        return MaxOfflineTransactionsProductionDefault;
    }
}
