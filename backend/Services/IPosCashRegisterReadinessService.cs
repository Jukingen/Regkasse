using System.Security.Claims;

namespace KasseAPI_Final.Services;

/// <summary>
/// POS entry orchestration: effective register resolution and controlled auto-open. Calls the same sole-register settings
/// helper as GET user settings: persist assignment only when exactly one cash_registers row exists and that row is Open.
/// </summary>
public interface IPosCashRegisterReadinessService
{
    Task<PosCashRegisterContextDto> EnsureReadyForPosAsync(
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
