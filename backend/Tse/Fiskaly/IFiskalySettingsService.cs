using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Tse.Fiskaly;

public interface IFiskalySettingsService
{
    FiskalySettingsDto GetSettings();

    Task<FiskalyStatusDto> GetStatusAsync(
        bool probeAuthentication = true,
        CancellationToken cancellationToken = default);

    Task<FiskalySettingsDto> UpdateEnabledAsync(
        bool enabled,
        string actorUserId,
        CancellationToken cancellationToken = default);
}
