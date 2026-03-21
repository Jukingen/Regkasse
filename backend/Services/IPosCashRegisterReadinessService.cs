using System.Security.Claims;

namespace KasseAPI_Final.Services;

/// <summary>
/// POS entry orchestration: effective register resolution, <c>nextAction</c>/message codes, and controlled auto-open.
/// Uses <see cref="ICashRegisterResolutionService.ApplySoleOpenRegisterAutoAssignmentIfNeededAsync"/> like the settings flow.
/// Open-register shift conflicts use the same predicate as payment and the POS picker (<see cref="CashRegisterShiftOccupancy.IsHeldByOtherUser"/>).
/// Payment creation does not call this service; payment authorizes <c>CashRegisterId</c> via <see cref="ICashRegisterResolutionService.ValidatePaymentRegisterAsync"/> only.
/// </summary>
public interface IPosCashRegisterReadinessService
{
    Task<PosCashRegisterContextDto> EnsureReadyForPosAsync(
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
