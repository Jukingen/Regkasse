using KasseAPI_Final.Tse.Fiskaly;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// fiskaly SIGN AT operations used by TSE provisioning and diagnostics.
/// TSS → SCU, client → cash register. RKSV compact JWS still goes through <c>SignaturePipeline</c>.
/// </summary>
public interface IFiskalyTseService
{
    Task<FiskalyAuthResult> AuthenticateAsync(CancellationToken cancellationToken = default);

    /// <summary>Create or retrieve an SCU (SIGN AT analog of TSS).</summary>
    Task<FiskalyScuInfo> CreateTssAsync(
        string tssId,
        string? vatId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Create or retrieve a cash register (SIGN AT analog of client).</summary>
    Task<FiskalyCashRegisterInfo> CreateClientAsync(
        string tssId,
        string clientId,
        CancellationToken cancellationToken = default);

    Task<FiskalySignedReceipt> SignTransactionAsync(
        string tssId,
        string txId,
        FiskalyTransactionData data,
        CancellationToken cancellationToken = default);

    Task<FiskalyResourceEnsureResult> EnsureResourcesForCashRegisterAsync(
        Guid tenantId,
        Guid cashRegisterId,
        string registerNumber,
        CancellationToken cancellationToken = default);
}
