using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Vouchers;

namespace KasseAPI_Final.Services;

public partial class PaymentService
{
    private const decimal VoucherMoneyTolerance = 0.01m;

    private static bool IsVoucherLegacyPayment(string legacyRaw) =>
        string.Equals(legacyRaw, ((int)PaymentMethod.Voucher).ToString(), StringComparison.Ordinal);

    /// <summary>Per-voucher redemption line after validation (tracked entities).</summary>
    private sealed class VoucherRedeemLine
    {
        public Voucher Voucher { get; init; } = null!;
        public decimal Amount { get; init; }
    }

    private async Task<(PaymentResult? Error, List<VoucherRedeemLine>? Lines)> BuildVoucherRedemptionPlanAsync(
        Guid tenantId,
        decimal fiscalTotal,
        PaymentMethodRequest payment,
        CancellationToken cancellationToken)
    {
        var aggregated = new Dictionary<string, decimal>(StringComparer.Ordinal);

        var hasMulti = payment.VoucherRedemptions != null && payment.VoucherRedemptions.Count > 0;
        if (hasMulti)
        {
            foreach (var row in payment.VoucherRedemptions!)
            {
                var n = VoucherCodeHasher.NormalizeCode(row.Code);
                if (string.IsNullOrEmpty(n))
                    return (InvalidVoucherPayment("Each voucher redemption requires a non-empty code."), null);
                if (row.Amount <= 0)
                    return (InvalidVoucherPayment("Voucher redemption amounts must be positive."), null);
                aggregated.TryGetValue(n, out var acc);
                aggregated[n] = acc + decimal.Round(row.Amount, 2, MidpointRounding.AwayFromZero);
            }
        }
        else if (!string.IsNullOrWhiteSpace(payment.VoucherCode))
        {
            var n = VoucherCodeHasher.NormalizeCode(payment.VoucherCode);
            if (string.IsNullOrEmpty(n))
                return (InvalidVoucherPayment("Voucher code is empty."), null);
            aggregated[n] = decimal.Round(fiscalTotal, 2, MidpointRounding.AwayFromZero);
        }
        else
            return (InvalidVoucherPayment("Voucher payment requires voucherCode or voucherRedemptions."), null);

        var sum = aggregated.Values.Sum();
        if (Math.Abs(sum - fiscalTotal) > VoucherMoneyTolerance)
            return (InvalidVoucherPayment("Voucher redemption total must match the fiscal sale total."), null);

        var lines = new List<VoucherRedeemLine>();
        foreach (var (codeNorm, amount) in aggregated.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var hash = VoucherCodeHasher.HashNormalized(codeNorm);
            var voucher = await _context.Vouchers
                .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.CodeHash == hash, cancellationToken)
                .ConfigureAwait(false);
            if (voucher == null)
                return (InvalidVoucherPayment("Voucher not found or not valid for this location."), null);

            if (voucher.Currency != "EUR")
            {
                _logger.LogWarning("Voucher {VoucherId} currency {Currency} is not supported for POS redemption (EUR only).", voucher.Id, voucher.Currency);
                return (InvalidVoucherPayment("Unsupported voucher currency for this register."), null);
            }

            var eval = VoucherValidationRules.Evaluate(voucher, DateTime.UtcNow);
            if (!eval.IsRedeemable)
                return (InvalidVoucherPayment(eval.ErrorMessage ?? "Voucher cannot be redeemed."), null);

            if (amount > voucher.RemainingAmount + VoucherMoneyTolerance)
                return (InvalidVoucherPayment("Voucher redemption exceeds remaining balance."), null);

            lines.Add(new VoucherRedeemLine { Voucher = voucher, Amount = amount });
        }

        lines.Sort((a, b) => a.Voucher.Id.CompareTo(b.Voucher.Id));
        return (null, lines);
    }

    private static PaymentResult InvalidVoucherPayment(string message) =>
        new()
        {
            Success = false,
            Message = message,
            Errors = { message },
            IsDeterministicFailure = true,
            DiagnosticCode = "VOUCHER_INVALID"
        };

    private async Task LockVoucherRowsForUpdateAsync(IReadOnlyList<Guid> voucherIdsSorted, CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational())
            return;

        foreach (var id in voucherIdsSorted)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM vouchers WHERE id = {id} FOR UPDATE",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<PaymentResult?> ApplyVoucherRedemptionsInCurrentTransactionAsync(
        Guid tenantId,
        string userId,
        PaymentDetails payment,
        Guid receiptId,
        IReadOnlyList<VoucherRedeemLine> lines,
        string? paymentIdempotencyKey,
        CancellationToken cancellationToken)
    {
        var sortedIds = lines.Select(l => l.Voucher.Id).Distinct().OrderBy(x => x).ToList();
        await LockVoucherRowsForUpdateAsync(sortedIds, cancellationToken).ConfigureAwait(false);

        foreach (var line in lines)
        {
            var v = await _context.Vouchers
                .FirstAsync(x => x.Id == line.Voucher.Id && x.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false);

            var eval = VoucherValidationRules.Evaluate(v, DateTime.UtcNow);
            if (!eval.IsRedeemable)
                return InvalidVoucherPayment(eval.ErrorMessage ?? "Voucher cannot be redeemed.");

            if (line.Amount > v.RemainingAmount + VoucherMoneyTolerance)
                return InvalidVoucherPayment("Voucher balance changed; redemption exceeds remaining balance.");

            var before = v.RemainingAmount;
            v.RemainingAmount = decimal.Round(before - line.Amount, 2, MidpointRounding.AwayFromZero);
            if (v.RemainingAmount < 0)
                return InvalidVoucherPayment("Voucher overdraft prevented.");

            v.Status = v.RemainingAmount <= 0
                ? VoucherStatus.Redeemed
                : v.RemainingAmount < v.InitialAmount
                    ? VoucherStatus.PartiallyRedeemed
                    : VoucherStatus.Active;

            var idem = BuildVoucherLedgerIdempotencyKey(paymentIdempotencyKey, payment.Id, v.Id, "redeem");
            var ledger = new VoucherLedgerEntry
            {
                TenantId = tenantId,
                VoucherId = v.Id,
                PaymentId = payment.Id,
                ReceiptId = receiptId,
                Type = VoucherTransactionType.Redeem,
                Amount = -line.Amount,
                BalanceAfter = v.RemainingAmount,
                CreatedByUserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                CorrelationId = null,
                IdempotencyKey = idem
            };
            _context.VoucherLedgerEntries.Add(ledger);
        }

        return null;
    }

    private static string BuildVoucherLedgerIdempotencyKey(string? paymentIdempotencyKey, Guid paymentId, Guid voucherId, string suffix)
    {
        var core = string.IsNullOrEmpty(paymentIdempotencyKey)
            ? $"{paymentId:N}"
            : $"{paymentIdempotencyKey.Trim()}:p";
        var key = $"{core}:vr:{voucherId:N}:{suffix}";
        return key.Length <= 128 ? key : key[..128];
    }

    /// <summary>
    /// Reverses voucher balance for fiscal storno of the original sale. Idempotent per redeem ledger row.
    /// </summary>
    private async Task ApplyVoucherRefundsForStornoAsync(
        Guid originalPaymentId,
        Guid stornoPaymentId,
        Guid stornoReceiptId,
        Guid tenantId,
        string userId,
        string? cancelIdempotencyKey,
        CancellationToken cancellationToken)
    {
        var redeemRows = await _context.VoucherLedgerEntries
            .Where(l => l.PaymentId == originalPaymentId && l.Type == VoucherTransactionType.Redeem)
            .OrderBy(l => l.VoucherId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (redeemRows.Count == 0)
            return;

        var distinctVoucherIds = redeemRows.Select(r => r.VoucherId).Distinct().OrderBy(x => x).ToList();
        await LockVoucherRowsForUpdateAsync(distinctVoucherIds, cancellationToken).ConfigureAwait(false);

        foreach (var redeem in redeemRows)
        {
            var refundKey = BuildStornoVoucherRefundIdempotencyKey(cancelIdempotencyKey, stornoPaymentId, redeem.Id);
            var exists = await _context.VoucherLedgerEntries.AnyAsync(
                l => l.IdempotencyKey == refundKey,
                cancellationToken).ConfigureAwait(false);
            if (exists)
                continue;

            var voucher = await _context.Vouchers.FirstAsync(v => v.Id == redeem.VoucherId, cancellationToken).ConfigureAwait(false);
            var credit = -redeem.Amount;
            if (credit <= 0)
            {
                _logger.LogWarning(
                    "Skipping voucher storno refund for redeem ledger {LedgerId}: non-positive credit {Credit}",
                    redeem.Id,
                    credit);
                continue;
            }

            voucher.RemainingAmount = decimal.Round(voucher.RemainingAmount + credit, 2, MidpointRounding.AwayFromZero);
            if (voucher.RemainingAmount > voucher.InitialAmount)
            {
                _logger.LogError(
                    "Voucher storno refund would exceed initial amount for voucher {VoucherId}; capping (data integrity risk).",
                    voucher.Id);
                voucher.RemainingAmount = voucher.InitialAmount;
            }

            voucher.Status = voucher.RemainingAmount <= 0
                ? VoucherStatus.Redeemed
                : voucher.RemainingAmount < voucher.InitialAmount
                    ? VoucherStatus.PartiallyRedeemed
                    : VoucherStatus.Active;

            _context.VoucherLedgerEntries.Add(new VoucherLedgerEntry
            {
                TenantId = voucher.TenantId,
                VoucherId = voucher.Id,
                PaymentId = stornoPaymentId,
                ReceiptId = stornoReceiptId,
                Type = VoucherTransactionType.Refund,
                Amount = credit,
                BalanceAfter = voucher.RemainingAmount,
                CreatedByUserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                CorrelationId = null,
                IdempotencyKey = refundKey
            });
        }
    }

    private static string BuildStornoVoucherRefundIdempotencyKey(string? cancelIdempotencyKey, Guid stornoPaymentId, Guid redeemLedgerId)
    {
        var core = string.IsNullOrEmpty(cancelIdempotencyKey) ? $"{stornoPaymentId:N}" : cancelIdempotencyKey.Trim();
        var key = $"{core}:vref:{redeemLedgerId:N}";
        return key.Length <= 128 ? key : key[..128];
    }
}
