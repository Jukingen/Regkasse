using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>
/// POS cashier requests for Mandanten-Admin to open a closed cash register.
/// </summary>
public interface ICashRegisterOpenRequestService
{
    Task<CashRegisterOpenRequestMutationResult> CreateAsync(
        string requesterUserId,
        CreateCashRegisterOpenRequestBody body,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashRegisterOpenRequestDto>> ListMineAsync(
        string requesterUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashRegisterOpenRequestDto>> ListAsync(
        string? status,
        Guid? tenantIdFilter,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<CashRegisterOpenRequestMutationResult> ApproveAsync(
        Guid requestId,
        string resolverUserId,
        string resolverRole,
        bool actorIsSuperAdmin,
        ResolveCashRegisterOpenRequestBody? body,
        CancellationToken cancellationToken = default);

    Task<CashRegisterOpenRequestMutationResult> DenyAsync(
        Guid requestId,
        string resolverUserId,
        string resolverRole,
        bool actorIsSuperAdmin,
        ResolveCashRegisterOpenRequestBody? body,
        CancellationToken cancellationToken = default);
}
