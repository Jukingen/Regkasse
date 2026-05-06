using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Vouchers;

/// <summary>Non-fiscal stored-value voucher sale (RKSV excludes issuance from DEP); no TSE.</summary>
public interface IVoucherIssuanceService
{
    Task<(IssueVoucherResponse? Response, string? ErrorCode)> IssueAsync(
        Guid tenantId,
        string userId,
        string userRole,
        IssueVoucherRequest request,
        CancellationToken cancellationToken = default);
}
