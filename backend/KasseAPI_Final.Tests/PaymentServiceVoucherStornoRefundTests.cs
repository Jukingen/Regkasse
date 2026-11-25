using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.Vouchers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Coverage for ApplyVoucherRefundsForStornoAsync.
/// Full storno restores voucher balance via Refund ledger rows. A refund on a voucher-paid sale (any amount, pure or mixed)
/// is rejected with VOUCHER_PARTIAL_REFUND_NOT_SUPPORTED.
/// Expired vouchers are still refunded on storno (expiry is a redemption-time rule).
/// </summary>
public sealed class PaymentServiceVoucherStornoRefundTests
{
    private const string CodeA = "GUT-TEST-001";
    private const string CodeB = "GUT-TEST-002";

    [Fact]
    public async Task Storno_WhenVoucherPayment_RestoresBalanceAndWritesRefundLedger()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, voucherId) =
            await SeedCatalogAndVoucherAsync(ctx, CodeA, remaining: 100m);
        var audit = PaymentServiceCoverageHarness.CreateAuditMock();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Audit = audit });

        var sale = await CreateVoucherSaleAsync(sut, customerId, productId, registerId, CodeA, 5m);
        Assert.True(sale.Success, sale.Message);

        var storno = await sut.CancelPaymentAsync(
            sale.Payment!.Id,
            "Kunde hat storniert",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);

        Assert.True(storno.Success, storno.Message + ": " + string.Join("; ", storno.Errors));
        var voucher = await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherId);
        Assert.Equal(100m, voucher.RemainingAmount);
        Assert.Equal(VoucherStatus.Active, voucher.Status);

        var refunds = await ctx.VoucherLedgerEntries.AsNoTracking()
            .Where(l => l.VoucherId == voucherId && l.Type == VoucherTransactionType.Refund)
            .ToListAsync();
        Assert.Single(refunds);
        Assert.Equal(5m, refunds[0].Amount);
        Assert.Equal(100m, refunds[0].BalanceAfter);
        Assert.Equal(storno.Payment!.Id, refunds[0].PaymentId);

        audit.Verify(x => x.LogPaymentOperationAsync(
                "PaymentReversal",
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<double?>()),
            Times.Once);
    }

    private const string VoucherRefundRejectedCode = "VOUCHER_PARTIAL_REFUND_NOT_SUPPORTED";

    [Fact]
    public async Task PartialRefund_WhenVoucherPayment_IsRejectedWithExplicitError()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, voucherId) =
            await SeedCatalogAndVoucherAsync(ctx, CodeA, remaining: 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var sale = await CreateVoucherSaleAsync(sut, customerId, productId, registerId, CodeA, 5m);
        Assert.True(sale.Success, sale.Message);
        var ledgerCountBefore = await ctx.VoucherLedgerEntries.CountAsync();

        var refund = await sut.RefundPaymentAsync(
            sale.Payment!.Id,
            2.50m,
            "Teilweise Reklamation",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: RefundReasonCode.CustomerComplaint);

        Assert.False(refund.Success);
        Assert.Equal(VoucherRefundRejectedCode, refund.DiagnosticCode);
        Assert.True(refund.IsDeterministicFailure);
        Assert.Contains("full storno", refund.Message, StringComparison.OrdinalIgnoreCase);

        // No balance change, no ledger entry.
        var voucher = await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherId);
        Assert.Equal(95m, voucher.RemainingAmount);
        Assert.Equal(ledgerCountBefore, await ctx.VoucherLedgerEntries.CountAsync());
        Assert.Empty(await ctx.VoucherLedgerEntries.AsNoTracking()
            .Where(l => l.Type == VoucherTransactionType.Refund)
            .ToListAsync());

        // Sale is not marked as refunded and no refund document exists.
        var original = await ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == sale.Payment.Id);
        Assert.Null(original.RefundedAt);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync(p => p.IsRefund));
    }

    [Fact]
    public async Task FullAmountRefund_WhenVoucherPayment_IsAlsoRejected()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, voucherId) =
            await SeedCatalogAndVoucherAsync(ctx, CodeA, remaining: 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var sale = await CreateVoucherSaleAsync(sut, customerId, productId, registerId, CodeA, 5m);
        Assert.True(sale.Success, sale.Message);

        var refund = await sut.RefundPaymentAsync(
            sale.Payment!.Id,
            5m,
            "Komplette Reklamation",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: RefundReasonCode.CustomerComplaint);

        Assert.False(refund.Success);
        Assert.Equal(VoucherRefundRejectedCode, refund.DiagnosticCode);
        var voucher = await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherId);
        Assert.Equal(95m, voucher.RemainingAmount);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync(p => p.IsRefund));
    }

    [Fact]
    public async Task PartialRefund_WhenMixedCashAndVoucherPayment_IsRejectedWithSameError()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, voucherId) =
            await SeedCatalogAndVoucherAsync(ctx, CodeA, remaining: 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        // Cash method + voucher redemption: PaymentMethodRaw is Cash, voucher portion is only visible in the ledger.
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 5m, method: "cash");
        request.Payment.Amount = 2m;
        request.Payment.VoucherRedemptions = [new VoucherRedemptionRequestItem { Code = CodeA, Amount = 3m }];
        var sale = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message + ": " + string.Join("; ", sale.Errors));
        var original = await ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == sale.Payment!.Id);
        Assert.Equal(((int)PaymentMethod.Cash).ToString(), original.PaymentMethodRaw);
        Assert.Equal(97m, (await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherId)).RemainingAmount);

        var refund = await sut.RefundPaymentAsync(
            sale.Payment!.Id,
            1m,
            "Teilweise Reklamation",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: RefundReasonCode.CustomerComplaint);

        Assert.False(refund.Success);
        Assert.Equal(VoucherRefundRejectedCode, refund.DiagnosticCode);
        Assert.Equal(97m, (await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherId)).RemainingAmount);
        Assert.Empty(await ctx.VoucherLedgerEntries.AsNoTracking()
            .Where(l => l.Type == VoucherTransactionType.Refund)
            .ToListAsync());
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync(p => p.IsRefund));
    }

    [Fact]
    public async Task Storno_AfterRejectedRefund_StillRestoresBalanceAndWritesRefundLedger()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, voucherId) =
            await SeedCatalogAndVoucherAsync(ctx, CodeA, remaining: 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var sale = await CreateVoucherSaleAsync(sut, customerId, productId, registerId, CodeA, 5m);
        Assert.True(sale.Success, sale.Message);

        var refund = await sut.RefundPaymentAsync(
            sale.Payment!.Id,
            2.50m,
            "Teilweise Reklamation",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: RefundReasonCode.CustomerComplaint);
        Assert.False(refund.Success);
        Assert.Equal(VoucherRefundRejectedCode, refund.DiagnosticCode);

        var storno = await sut.CancelPaymentAsync(
            sale.Payment.Id,
            "Kunde hat storniert",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);

        Assert.True(storno.Success, storno.Message + ": " + string.Join("; ", storno.Errors));
        var voucher = await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherId);
        Assert.Equal(100m, voucher.RemainingAmount);
        Assert.Equal(VoucherStatus.Active, voucher.Status);

        var refunds = await ctx.VoucherLedgerEntries.AsNoTracking()
            .Where(l => l.VoucherId == voucherId && l.Type == VoucherTransactionType.Refund)
            .ToListAsync();
        var entry = Assert.Single(refunds);
        Assert.Equal(5m, entry.Amount);
        Assert.Equal(100m, entry.BalanceAfter);
        Assert.Equal(storno.Payment!.Id, entry.PaymentId);
    }

    [Fact]
    public async Task Storno_WhenMultipleVouchers_RefundsEach()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, voucherA) =
            await SeedCatalogAndVoucherAsync(ctx, CodeA, remaining: 100m);
        var voucherB = await AddVoucherAsync(ctx, CodeB, remaining: 50m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 5m, method: "voucher");
        request.Payment.VoucherRedemptions =
        [
            new VoucherRedemptionRequestItem { Code = CodeA, Amount = 3m },
            new VoucherRedemptionRequestItem { Code = CodeB, Amount = 2m }
        ];
        var sale = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message + ": " + string.Join("; ", sale.Errors));

        var storno = await sut.CancelPaymentAsync(
            sale.Payment!.Id,
            "Kunde hat storniert",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);
        Assert.True(storno.Success, storno.Message);

        var a = await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherA);
        var b = await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherB);
        Assert.Equal(100m, a.RemainingAmount);
        Assert.Equal(50m, b.RemainingAmount);
        Assert.Equal(2, await ctx.VoucherLedgerEntries.CountAsync(l => l.Type == VoucherTransactionType.Refund));
    }

    [Fact]
    public async Task Storno_WhenVoucherExpiredAfterSale_StillRestoresBalance()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, voucherId) =
            await SeedCatalogAndVoucherAsync(ctx, CodeA, remaining: 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await CreateVoucherSaleAsync(sut, customerId, productId, registerId, CodeA, 5m);
        Assert.True(sale.Success, sale.Message);

        var voucher = await ctx.Vouchers.FirstAsync(v => v.Id == voucherId);
        voucher.ExpiresAtUtc = DateTime.UtcNow.AddDays(-1);
        voucher.Status = VoucherStatus.Expired;
        await ctx.SaveChangesAsync();

        var storno = await sut.CancelPaymentAsync(
            sale.Payment!.Id,
            "Kunde hat storniert",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);

        Assert.True(storno.Success, storno.Message);
        var restored = await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherId);
        Assert.Equal(100m, restored.RemainingAmount);
    }

    [Fact]
    public async Task Storno_WhenCashPayment_DoesNotWriteVoucherRefund()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await SeedCatalogAndVoucherAsync(ctx, CodeA, remaining: 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 5m),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);

        var storno = await sut.CancelPaymentAsync(
            sale.Payment!.Id,
            "Kunde hat storniert",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);

        Assert.True(storno.Success, storno.Message);
        Assert.Empty(await ctx.VoucherLedgerEntries.AsNoTracking()
            .Where(l => l.Type == VoucherTransactionType.Refund)
            .ToListAsync());
    }

    [Fact]
    public async Task Storno_WhenRemainingAlreadyAtInitial_CapsBalance()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, voucherId) =
            await SeedCatalogAndVoucherAsync(ctx, CodeA, remaining: 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await CreateVoucherSaleAsync(sut, customerId, productId, registerId, CodeA, 5m);
        Assert.True(sale.Success, sale.Message);

        var voucher = await ctx.Vouchers.FirstAsync(v => v.Id == voucherId);
        voucher.RemainingAmount = voucher.InitialAmount;
        await ctx.SaveChangesAsync();

        var storno = await sut.CancelPaymentAsync(
            sale.Payment!.Id,
            "Kunde hat storniert",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);

        Assert.True(storno.Success, storno.Message);
        var capped = await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherId);
        Assert.Equal(100m, capped.RemainingAmount);
        Assert.Single(await ctx.VoucherLedgerEntries.AsNoTracking()
            .Where(l => l.Type == VoucherTransactionType.Refund)
            .ToListAsync());
    }

    [Fact]
    public async Task Storno_WhenRedeemAmountNonPositive_SkipsRefund()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, voucherId) =
            await SeedCatalogAndVoucherAsync(ctx, CodeA, remaining: 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await CreateVoucherSaleAsync(sut, customerId, productId, registerId, CodeA, 5m);
        Assert.True(sale.Success, sale.Message);

        var redeem = await ctx.VoucherLedgerEntries
            .FirstAsync(l => l.PaymentId == sale.Payment!.Id && l.Type == VoucherTransactionType.Redeem);
        redeem.Amount = 0m;
        await ctx.SaveChangesAsync();

        var storno = await sut.CancelPaymentAsync(
            sale.Payment!.Id,
            "Kunde hat storniert",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);

        Assert.True(storno.Success, storno.Message);
        var voucher = await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherId);
        Assert.Equal(95m, voucher.RemainingAmount);
        Assert.Empty(await ctx.VoucherLedgerEntries.AsNoTracking()
            .Where(l => l.Type == VoucherTransactionType.Refund)
            .ToListAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenVoucherCodeUnknown_ReturnsVoucherInvalid()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await SeedCatalogAndVoucherAsync(ctx, CodeA, remaining: 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await CreateVoucherSaleAsync(sut, customerId, productId, registerId, "GUT-UNKNOWN", 5m);

        Assert.False(result.Success);
        Assert.Equal("VOUCHER_INVALID", result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    private static async Task<(Guid CustomerId, Guid ProductId, Guid CashRegisterId, Guid VoucherId)> SeedCatalogAndVoucherAsync(
        KasseAPI_Final.Data.AppDbContext ctx,
        string code,
        decimal remaining)
    {
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, unitPrice: 5m);
        var voucherId = await AddVoucherAsync(ctx, code, remaining);
        return (customerId, productId, registerId, voucherId);
    }

    private static async Task<Guid> AddVoucherAsync(
        KasseAPI_Final.Data.AppDbContext ctx,
        string code,
        decimal remaining)
    {
        var voucherId = Guid.NewGuid();
        var hash = VoucherCodeHasher.HashNormalized(VoucherCodeHasher.NormalizeCode(code));
        ctx.Vouchers.Add(new Voucher
        {
            Id = voucherId,
            TenantId = SystemTenantIds.Platform,
            CodeHash = hash,
            MaskedCode = "****" + code[^3..],
            InitialAmount = remaining,
            RemainingAmount = remaining,
            Currency = "EUR",
            Status = VoucherStatus.Active,
            ValidFromUtc = DateTime.UtcNow.AddDays(-2),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(10),
            CreatedByUserId = PaymentServiceCoverageHarness.CashierId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        return voucherId;
    }

    private static Task<PaymentResult> CreateVoucherSaleAsync(
        KasseAPI_Final.Services.PaymentService sut,
        Guid customerId,
        Guid productId,
        Guid registerId,
        string code,
        decimal total)
    {
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: total, method: "voucher");
        request.Payment.VoucherCode = code;
        request.Payment.TseRequired = false;
        return sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);
    }
}
