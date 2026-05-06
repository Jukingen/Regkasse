using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Vouchers;

/// <summary>Admin back-office voucher issuance, listing, cancellation, and ledger reads.</summary>
public interface IAdminVoucherService
{
    Task<AdminVoucherListResponse> ListAsync(Guid tenantId, int page, int pageSize, string? q, CancellationToken cancellationToken = default);

    Task<AdminVoucherDetailDto?> GetDetailAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminVoucherLedgerLineDto>> GetLedgerAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    Task<(CreateAdminVoucherResponse? Response, string? ErrorCode)> CreateAsync(
        Guid tenantId,
        string userId,
        CreateAdminVoucherRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? ErrorCode)> CancelAsync(
        Guid tenantId,
        string userId,
        Guid id,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether the provided code matches the voucher's stored hash. Plaintext is never stored or returned.
    /// </summary>
    /// <returns>(response, errorCode). errorCode: NOT_FOUND, CODE_REQUIRED, or null on success.</returns>
    Task<(VerifyAdminVoucherCodeResponse? Response, string? ErrorCode)> VerifyCodeMatchesAsync(
        Guid tenantId,
        Guid id,
        string code,
        CancellationToken cancellationToken = default);
}
