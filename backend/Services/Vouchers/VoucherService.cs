using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

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
            _logger.LogDebug(
                "Voucher validate: no row for tenant {TenantId} (codeHashCorrelationPrefix={HashPrefix})",
                tenantId,
                VoucherCodeHasher.HashCorrelationPrefix(normalized));
            return VoucherValidateResponse.NotFound();
        }

        var utcNow = DateTime.UtcNow;
        var effective = VoucherValidationRules.Evaluate(voucher, utcNow, tenantId, optionalAmount);
        if (!effective.IsRedeemable)
        {
            return VoucherValidateResponse.Fail(
                effective.ErrorCode ?? VoucherValidateErrorCodes.NotRedeemable,
                effective.ErrorMessage ?? VoucherValidationRules.NotValidForLocationMessage);
        }

        var maxRedeem = voucher.RemainingAmount;
        if (optionalAmount is { } opt && opt > 0)
            maxRedeem = Math.Min(voucher.RemainingAmount, decimal.Round(opt, 2, MidpointRounding.AwayFromZero));

        return VoucherValidateResponse.Valid(
            voucher.Status.ToString(),
            voucher.RemainingAmount,
            maxRedeem,
            voucher.ExpiresAtUtc,
            voucher.MaskedCode);
    }
}

/// <summary>Shared RKSV-oriented rules for POS validate and payment redemption (Mehrzweckgutschein).</summary>
public static class VoucherValidationRules
{
    /// <summary>Money comparison tolerance (matches payment voucher plan).</summary>
    public const decimal MoneyTolerance = 0.01m;

    public const string NotValidForLocationMessage = "Voucher not found or not valid for this location.";
    public const string ExpiredMessage = "Voucher has expired.";
    public const string NoRemainingBalanceMessage = "Voucher has no remaining balance.";
    public const string ExceedsRemainingBalanceMessage = "Voucher redemption exceeds remaining balance.";

    public sealed record Evaluation(bool IsRedeemable, string? ErrorCode, string? ErrorMessage);

    /// <param name="requestedRedeemAmount">Optional planned redemption; if set, must not exceed remaining balance (after rounding).</param>
    public static Evaluation Evaluate(
        Voucher voucher,
        DateTime utcNow,
        Guid expectedTenantId,
        decimal? requestedRedeemAmount = null)
    {
        if (expectedTenantId == Guid.Empty || voucher.TenantId == Guid.Empty || voucher.TenantId != expectedTenantId)
            return new Evaluation(false, VoucherValidateErrorCodes.NotFound, NotValidForLocationMessage);

        if (voucher.Status == VoucherStatus.Cancelled)
            return new Evaluation(false, VoucherValidateErrorCodes.NotFound, NotValidForLocationMessage);

        if (utcNow < voucher.ValidFromUtc)
            return new Evaluation(false, VoucherValidateErrorCodes.NotFound, NotValidForLocationMessage);

        if (utcNow >= voucher.ExpiresAtUtc || voucher.Status == VoucherStatus.Expired)
            return new Evaluation(false, VoucherValidateErrorCodes.Expired, ExpiredMessage);

        if (voucher.RemainingAmount <= 0 || voucher.Status == VoucherStatus.Redeemed)
            return new Evaluation(false, VoucherValidateErrorCodes.NoBalance, NoRemainingBalanceMessage);

        if (voucher.Status != VoucherStatus.Active && voucher.Status != VoucherStatus.PartiallyRedeemed)
            return new Evaluation(false, VoucherValidateErrorCodes.NotFound, NotValidForLocationMessage);

        if (requestedRedeemAmount is { } raw && raw > 0)
        {
            var requested = decimal.Round(raw, 2, MidpointRounding.AwayFromZero);
            if (requested > voucher.RemainingAmount + MoneyTolerance)
                return new Evaluation(false, VoucherValidateErrorCodes.AmountExceedsBalance, ExceedsRemainingBalanceMessage);
        }

        return new Evaluation(true, null, null);
    }
}
