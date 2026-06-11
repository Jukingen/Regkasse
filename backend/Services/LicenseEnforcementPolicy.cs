using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using Microsoft.Extensions.Hosting;

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
    /// Resolves the NonFiscalPending queue cap for a cash register.
    /// Development/Demo hosts use <see cref="MaxOfflineTransactionsUnlimited"/>; production uses configured TSE options (default 50).
    /// </summary>
    public static int GetMaxOfflineTransactionsPerCashRegister(
        IHostEnvironment? environment,
        TseOptions? tseOptions = null,
        IDevelopmentModeService? developmentMode = null,
        LicenseOptions? licenseOptions = null)
    {
        if (ShouldDisableEnforcement(environment, tseOptions, developmentMode, licenseOptions))
            return MaxOfflineTransactionsUnlimited;

        var configured = tseOptions?.MaxOfflineTransactionsPerCashRegister
                         ?? MaxOfflineTransactionsProductionDefault;
        return Math.Clamp(configured, 1, 10_000);
    }
}
