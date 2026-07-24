using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Optional simulated blockchain anchoring of TSE signature hashes.
/// Does not replace RKSV/TSE signing and is not Finanzamt proof.
/// </summary>
public interface ITseBlockchainService
{
    Task<TseBlockchainRecordDto> StoreSignatureAsync(
        TseBlockchainSignatureDataDto signature,
        CancellationToken cancellationToken = default);

    Task<TseBlockchainVerificationResultDto> VerifySignatureAsync(
        Guid signatureId,
        CancellationToken cancellationToken = default);

    Task<TseBlockchainStatusDto> GetBlockchainStatusAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseBlockchainTransactionDto>> GetTransactionsAsync(
        Guid tenantId,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Advances simulated ledger tip / refreshes connection status.</summary>
    Task<TseBlockchainStatusDto> SyncBlockchainAsync(
        CancellationToken cancellationToken = default);
}
