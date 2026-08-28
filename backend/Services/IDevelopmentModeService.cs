using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public interface IDevelopmentModeService
{
    Task<DevelopmentModeSettings> GetSettingsAsync();
    Task UpdateSettingsAsync(DevelopmentModeSettings settings, Guid? updatedByUserId);

    /// <summary>Clears the in-memory snapshot and reloads the singleton row from the database (e.g. after admin update).</summary>
    Task ReloadSettingsCacheAsync(CancellationToken cancellationToken = default);

    bool IsDevelopmentModeEnabled();
    bool ShouldBypassLicense();
    bool ShouldBypassNtpCheck();
    /// <summary>
    /// True when TSE health probes should skip hardware. Combines DevelopmentOptions.BypassTseInDevelopment,
    /// FA development-mode BypassTseCheck, and the RKSV overlay (TseMode=Real never bypasses).
    /// Effective only on a Development host.
    /// </summary>
    bool ShouldBypassTseCheck();
    bool ShouldSimulateOffline();
    bool ShouldForceOnline();
    int GetValidDays();
    string[] GetFeatures();
}
