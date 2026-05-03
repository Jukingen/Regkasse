using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Vouchers;

/// <summary>Read-only voucher validation for POS (no balance mutation).</summary>
public interface IVoucherService
{
    Task<VoucherValidateResponse> ValidateVoucherByCodeAsync(
        Guid tenantId,
        string voucherCode,
        decimal? optionalAmount,
        CancellationToken cancellationToken = default);
}
