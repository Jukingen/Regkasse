using System.Security.Claims;

namespace KasseAPI_Final.Services;

/// <summary>
/// POS entry orchestration: effective register resolution, <c>nextAction</c>/message codes, and controlled auto-open.
/// Uses <see cref="ICashRegisterResolutionService.ApplySoleOpenRegisterAutoAssignmentIfNeededAsync"/> (same helper as POST <c>api/user/settings/bootstrap</c>).
/// Open-register shift conflicts use the same predicate as payment and the POS picker (<see cref="CashRegisterShiftOccupancy.IsHeldByOtherUser"/>).
/// Payment creation does not call this service; payment authorizes <c>CashRegisterId</c> via <see cref="ICashRegisterResolutionService.ValidatePaymentRegisterAsync"/> and re-checks at DB commit via <see cref="ICashRegisterResolutionService.ValidatePaymentRegisterForCommitAsync"/>.
/// </summary>
public interface IPosCashRegisterReadinessService
{
    Task<PosCashRegisterContextDto> EnsureReadyForPosAsync(
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only readiness snapshot for polling (<c>GET /api/pos/status/overview</c>). Does not auto-open or persist assignment.
    /// </summary>
    Task<PosCashRegisterContextDto> GetReadinessSnapshotForPosAsync(
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
