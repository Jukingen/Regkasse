using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Vouchers;

public class VoucherService : IVoucherService
{
    private readonly AppDbContext _context;
    private readonly ILogger<VoucherService> _logger;

    public VoucherService(AppDbContext context, ILogger<VoucherService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<VoucherValidateResponse> ValidateVoucherByCodeAsync(
        Guid tenantId,
        string voucherCode,
        decimal? optionalAmount,
        CancellationToken cancellationToken = default)
    {
        var normalized = VoucherCodeHasher.NormalizeCode(voucherCode);
        if (string.IsNullOrEmpty(normalized))
        {
            return VoucherValidateResponse.Fail(VoucherValidateErrorCodes.InvalidCode, "Voucher code is required.");
        }

        var hash = VoucherCodeHasher.HashNormalized(normalized);
        var voucher = await _context.Vouchers.AsNoTracking()
            .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.CodeHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (voucher == null)
        {
            _logger.LogDebug("Voucher validate: no row for tenant {TenantId} (hash prefix {Prefix})", tenantId, hash[..Math.Min(8, hash.Length)]);
            return VoucherValidateResponse.NotFound();
        }

        var utcNow = DateTime.UtcNow;
        var effective = VoucherValidationRules.Evaluate(voucher, utcNow);
        if (!effective.IsRedeemable)
        {
            return VoucherValidateResponse.Fail(
                effective.ErrorCode ?? VoucherValidateErrorCodes.NotRedeemable,
                effective.ErrorMessage ?? "Voucher cannot be redeemed.");
        }

        var maxRedeem = voucher.RemainingAmount;
        if (optionalAmount.HasValue && optionalAmount.Value > 0)
            maxRedeem = Math.Min(voucher.RemainingAmount, decimal.Round(optionalAmount.Value, 2, MidpointRounding.AwayFromZero));

        return VoucherValidateResponse.Valid(
            voucher.Status.ToString(),
            voucher.RemainingAmount,
            maxRedeem,
            voucher.ExpiresAtUtc,
            voucher.MaskedCode);
    }
}

/// <summary>Shared rules for validate + payment redeem paths.</summary>
public static class VoucherValidationRules
{
    public sealed record Evaluation(bool IsRedeemable, string? ErrorCode, string? ErrorMessage);

    public static Evaluation Evaluate(Voucher voucher, DateTime utcNow)
    {
        if (voucher.TenantId == Guid.Empty)
            return new Evaluation(false, VoucherValidateErrorCodes.NotRedeemable, "Invalid voucher context.");

        if (voucher.Status == VoucherStatus.Cancelled)
            return new Evaluation(false, VoucherValidateErrorCodes.Cancelled, "Voucher is cancelled.");

        if (voucher.Status == VoucherStatus.Redeemed)
            return new Evaluation(false, VoucherValidateErrorCodes.Redeemed, "Voucher is fully redeemed.");

        if (utcNow < voucher.ValidFromUtc)
            return new Evaluation(false, VoucherValidateErrorCodes.NotYetValid, "Voucher is not valid yet.");

        if (utcNow > voucher.ExpiresAtUtc)
            return new Evaluation(false, VoucherValidateErrorCodes.Expired, "Voucher has expired.");

        if (voucher.Status == VoucherStatus.Expired)
            return new Evaluation(false, VoucherValidateErrorCodes.Expired, "Voucher has expired.");

        if (voucher.RemainingAmount <= 0)
            return new Evaluation(false, VoucherValidateErrorCodes.NoBalance, "Voucher has no remaining balance.");

        return new Evaluation(true, null, null);
    }
}
