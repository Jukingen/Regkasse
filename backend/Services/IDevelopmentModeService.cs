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
    bool ShouldBypassTseCheck();
    bool ShouldSimulateOffline();
    bool ShouldForceOnline();
    int GetValidDays();
    string[] GetFeatures();
}
