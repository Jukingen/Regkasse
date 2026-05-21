using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.AdminCashRegisters;

public interface ICashRegisterDecommissionService
{
    bool IsHardDeleteAllowed();

    Task<DecommissionCashRegisterResponse> DecommissionAsync(
        Guid cashRegisterId,
        string? reason,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task HardDeleteAsync(
        Guid cashRegisterId,
        string confirmPhrase,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);
}
