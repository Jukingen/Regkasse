using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Preorder;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Services.Vouchers;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Direct edge cases for the services extracted from PaymentService.
/// Voucher and storno failures are deterministic <see cref="PaymentResult"/> values (the HTTP layer maps the storno conflict to 409).
/// The daily allowance cap is applied while benefits are evaluated, so no usage delta is emitted once the limit is reached.
/// </summary>
public sealed class PaymentVoucherLedgerServiceTests
{
    [Fact]
    public async Task BuildVoucherRedemptionPlanAsync_WhenVoucherPayloadEmpty_WritesNothing()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (_, _, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = new PaymentVoucherLedgerService(ctx, NullLogger.Instance);

        var (error, lines) = await sut.BuildVoucherRedemptionPlanAsync(
            SystemTenantIds.Platform,
            registerId,
            10m,
            new PaymentMethodRequest { Method = "voucher" },
            CancellationToken.None);

        Assert.Null(lines);
        Assert.NotNull(error);
        Assert.False(error!.Success);
        Assert.Equal("VOUCHER_INVALID", error.DiagnosticCode);
        Assert.Contains("voucherCode", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, await ctx.Vouchers.CountAsync());
        Assert.Equal(0, await ctx.VoucherLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task BuildVoucherRedemptionPlanAsync_WhenCodeUnknown_ReturnsVoucherInvalid()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (_, _, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = new PaymentVoucherLedgerService(ctx, NullLogger.Instance);

        var (error, lines) = await sut.BuildVoucherRedemptionPlanAsync(
            SystemTenantIds.Platform,
            registerId,
            10m,
            new PaymentMethodRequest { Method = "voucher", VoucherCode = "NO-SUCH-CODE" },
            CancellationToken.None);

        Assert.Null(lines);
        Assert.NotNull(error);
        Assert.Equal("VOUCHER_INVALID", error!.DiagnosticCode);
        Assert.Equal(VoucherValidationRules.NotValidForLocationMessage, error.Message);
        Assert.Equal(0, await ctx.VoucherLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task ApplyVoucherRedemptionsInCurrentTransactionAsync_WhenBalanceInsufficient_ReturnsVoucherInvalid()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var voucherId = await PaymentServiceCoverageHarness.AddVoucherAsync(ctx, "GUT-LOW", remaining: 5m);
        var voucher = await ctx.Vouchers.FirstAsync(v => v.Id == voucherId);
        var sut = new PaymentVoucherLedgerService(ctx, NullLogger.Instance);
        var payment = new PaymentDetails
        {
            CustomerName = "Gast",
            CashierId = PaymentServiceCoverageHarness.CashierId
        };

        var error = await sut.ApplyVoucherRedemptionsInCurrentTransactionAsync(
            SystemTenantIds.Platform,
            PaymentServiceCoverageHarness.CashierId,
            payment,
            Guid.NewGuid(),
            [new VoucherRedeemLine { Voucher = voucher, Amount = 20m }],
            "idem-1",
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal("VOUCHER_INVALID", error!.DiagnosticCode);
        Assert.Equal(VoucherValidationRules.ExceedsRemainingBalanceMessage, error.Message);
        Assert.Equal(5m, voucher.RemainingAmount);
        Assert.Equal(0, await ctx.VoucherLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task ApplyVoucherRefundsForStornoAsync_RestoresBalance()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var voucherId = await PaymentServiceCoverageHarness.AddVoucherAsync(ctx, "GUT-RESTORE", remaining: 100m);
        var voucher = await ctx.Vouchers.FirstAsync(v => v.Id == voucherId);
        voucher.RemainingAmount = 80m;
        voucher.Status = VoucherStatus.PartiallyRedeemed;
        var originalPaymentId = Guid.NewGuid();
        ctx.VoucherLedgerEntries.Add(new VoucherLedgerEntry
        {
            TenantId = SystemTenantIds.Platform,
            VoucherId = voucherId,
            PaymentId = originalPaymentId,
            Type = VoucherTransactionType.Redeem,
            Amount = -20m,
            BalanceAfter = 80m,
            CreatedByUserId = PaymentServiceCoverageHarness.CashierId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        var sut = new PaymentVoucherLedgerService(ctx, NullLogger.Instance);
        var stornoPaymentId = Guid.NewGuid();

        await sut.ApplyVoucherRefundsForStornoAsync(
            originalPaymentId,
            stornoPaymentId,
            Guid.NewGuid(),
            SystemTenantIds.Platform,
            PaymentServiceCoverageHarness.CashierId,
            cancelIdempotencyKey: null,
            CancellationToken.None);
        await ctx.SaveChangesAsync();

        var restored = await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherId);
        Assert.Equal(100m, restored.RemainingAmount);
        Assert.Equal(VoucherStatus.Active, restored.Status);
        var refund = await ctx.VoucherLedgerEntries.AsNoTracking()
            .SingleAsync(l => l.Type == VoucherTransactionType.Refund);
        Assert.Equal(20m, refund.Amount);
        Assert.Equal(100m, refund.BalanceAfter);
        Assert.Equal(stornoPaymentId, refund.PaymentId);
    }
}

public sealed class PaymentStornoServiceTests
{
    [Fact]
    public async Task CreateStornoReversalAsync_WhenOriginalIsNotPersisted_ReturnsReceiptRequired()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (_, _, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = CreateStornoService(ctx);
        var missing = new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerName = "Gast",
            CashierId = PaymentServiceCoverageHarness.CashierId,
            CashRegisterId = registerId,
            TotalAmount = 10m,
            TaxAmount = 0.91m,
            IsActive = true
        };

        var result = await sut.CreateStornoReversalAsync(
            missing,
            "Original fehlt",
            PaymentServiceCoverageHarness.CashierId,
            cancelIdempotencyKey: null,
            StornoReason.KundeStorniert);

        Assert.False(result.Success);
        Assert.True(result.IsDeterministicFailure);
        Assert.Equal("STORNO_ORIGINAL_RECEIPT_REQUIRED", result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync(p => p.IsStorno));
        Assert.Equal(0, await ctx.Invoices.CountAsync());
    }

    [Fact]
    public async Task CreateStornoReversalAsync_WhenStornoAlreadyExists_ReturnsConflict()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var payments = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await payments.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);

        var first = await payments.CancelPaymentAsync(
            sale.Payment!.Id,
            "Kunde hat storniert",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);
        Assert.True(first.Success, first.Message);

        var original = await ctx.PaymentDetails.FirstAsync(p => p.Id == sale.Payment.Id);
        var sut = CreateStornoService(ctx);
        var conflict = await sut.CreateStornoReversalAsync(
            original,
            "Zweiter Versuch",
            PaymentServiceCoverageHarness.CashierId,
            cancelIdempotencyKey: null,
            StornoReason.KundeStorniert);

        Assert.False(conflict.Success);
        Assert.True(conflict.IsDeterministicFailure);
        Assert.Equal("STORNO_ALREADY_EXISTS", conflict.DiagnosticCode);
        Assert.Equal(1, await ctx.PaymentDetails.CountAsync(p => p.IsStorno && p.OriginalPaymentId == sale.Payment.Id));
    }

    [Fact]
    public async Task CancelPaymentAsync_WhenPendingPreorder_CancelsIt()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var preorders = new PreorderService(
            ctx,
            TenantTestDoubles.PrimaryTenantResolver,
            NullLogger<PreorderService>.Instance);
        var payments = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Preorder = preorders });
        var sale = await payments.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);

        ctx.Orders.Add(new Order
        {
            OrderId = "PRE-1",
            OrderDate = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            TotalAmount = sale.Payment!.TotalAmount,
            TenantId = SystemTenantIds.Platform,
            IsPreorder = true,
            PreorderStatus = PreorderStatuses.Pending,
            SourcePaymentId = sale.Payment.Id
        });
        await ctx.SaveChangesAsync();

        var storno = await payments.CancelPaymentAsync(
            sale.Payment.Id,
            "Kunde hat storniert",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);

        Assert.True(storno.Success, storno.Message + ": " + string.Join("; ", storno.Errors));
        var order = await ctx.Orders.AsNoTracking().SingleAsync(o => o.SourcePaymentId == sale.Payment.Id);
        Assert.Equal(PreorderStatuses.Cancelled, order.PreorderStatus);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public async Task CancelPaymentAsync_RefundsCardAfterFiscalCommit()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var intentId = Guid.NewGuid();
        var stornoVisibleAtGateway = false;
        var creditNoteVisibleAtGateway = false;
        var card = new Mock<ICardPaymentService>();
        card.Setup(c => c.ValidateForFiscalPaymentAsync(intentId, 10m, registerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new CardPaymentTransaction
            {
                Id = intentId,
                TenantId = SystemTenantIds.Platform,
                Amount = 10m,
                Currency = "EUR",
                CashRegisterId = registerId,
                Status = CardPaymentTransactionStatuses.Succeeded,
                Gateway = "Mock"
            }, (string?)null, (string?)null));
        card.Setup(c => c.LinkToPaymentAsync(intentId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        card.Setup(c => c.RefundForFiscalPaymentAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Returns((Guid paymentId, decimal _, CancellationToken _) =>
            {
                stornoVisibleAtGateway = ctx.PaymentDetails.Any(p => p.IsStorno && p.OriginalPaymentId == paymentId);
                creditNoteVisibleAtGateway = ctx.Invoices.Any(i =>
                    i.DocumentType == DocumentType.CreditNote && i.TotalAmount < 0);
                return Task.FromResult((true, (string?)null, (string?)null));
            });

        var payments = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Card = card.Object });
        var sale = await payments.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(
                customerId, productId, registerId, method: "card", cardPaymentIntentId: intentId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message + ": " + string.Join("; ", sale.Errors));

        var storno = await payments.CancelPaymentAsync(
            sale.Payment!.Id,
            "Kunde hat storniert",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: CancellationReasonCode.CustomerRequest);

        Assert.True(storno.Success, storno.Message + ": " + string.Join("; ", storno.Errors));
        Assert.True(stornoVisibleAtGateway);
        Assert.True(creditNoteVisibleAtGateway);
        card.Verify(
            c => c.RefundForFiscalPaymentAsync(sale.Payment.Id, sale.Payment.TotalAmount, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static PaymentStornoService CreateStornoService(AppDbContext ctx)
    {
        PaymentTenantMatch belongs = (_, _) => Task.FromResult(true);
        AllocateCountryBelegNr allocate = (_, _, _, _) => Task.FromResult("AT-TEST-1");
        RunPaymentHook hook = (_, _, _, _, _) => Task.CompletedTask;
        LogPaymentAudit audit = (_, _, _, _, _, _, _, _, _, _) => Task.CompletedTask;
        LogFiskalyCancellationAudit fiskaly = (_, _, _, _) => Task.CompletedTask;
        return new PaymentStornoService(
            ctx,
            new GenericRepository<PaymentDetails>(ctx, NullLogger<GenericRepository<PaymentDetails>>.Instance),
            Mock.Of<IUserService>(),
            Mock.Of<IReceiptService>(),
            Mock.Of<ICompanyProfileProvider>(),
            TenantTestDoubles.PrimaryTenantResolver,
            Mock.Of<ICountryStrategyContext>(),
            new InventoryOptions(),
            Mock.Of<ITseService>(),
            new PaymentVoucherLedgerService(ctx, NullLogger.Instance),
            Mock.Of<IPaymentReversalApprovalService>(),
            NullLogger.Instance,
            cardPaymentService: null,
            preorderService: null,
            belongs,
            allocate,
            hook,
            audit,
            fiskaly);
    }

}

public sealed class PaymentRefundServiceTests
{
    [Fact]
    public async Task RefundPaymentAsync_WhenFullRefund_CreatesCreditNote()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var payments = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await payments.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);

        var refund = await payments.RefundPaymentAsync(
            sale.Payment!.Id,
            sale.Payment.TotalAmount,
            "Volle Reklamation",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: RefundReasonCode.CustomerComplaint);

        Assert.True(refund.Success, refund.Message + ": " + string.Join("; ", refund.Errors));
        Assert.Equal(-sale.Payment.TotalAmount, refund.Payment!.TotalAmount);
        Assert.Equal(-sale.Payment.TaxAmount, refund.Payment.TaxAmount);
        var note = await ctx.Invoices.AsNoTracking().SingleAsync(i => i.DocumentType == DocumentType.CreditNote);
        var originalInvoice = await ctx.Invoices.AsNoTracking().SingleAsync(i => i.SourcePaymentId == sale.Payment.Id);
        Assert.Equal(refund.Payment.Id, note.SourcePaymentId);
        Assert.Equal(-sale.Payment.TotalAmount, note.TotalAmount);
        Assert.Equal(originalInvoice.Id, note.OriginalInvoiceId);
    }

    [Fact]
    public async Task RefundPaymentAsync_WhenPartialRefund_ScalesTaxProportionally()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var payments = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await payments.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 10m),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);
        const decimal refundAmount = 4m;

        var refund = await payments.RefundPaymentAsync(
            sale.Payment!.Id,
            refundAmount,
            "Teilreklamation",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: RefundReasonCode.CustomerComplaint);

        Assert.True(refund.Success, refund.Message + ": " + string.Join("; ", refund.Errors));
        var ratio = refundAmount / sale.Payment.TotalAmount;
        Assert.Equal(-refundAmount, refund.Payment!.TotalAmount);
        Assert.Equal(-sale.Payment.TaxAmount * ratio, refund.Payment.TaxAmount);
        var note = await ctx.Invoices.AsNoTracking().SingleAsync(i => i.DocumentType == DocumentType.CreditNote);
        Assert.Equal(-refundAmount, note.TotalAmount);
        Assert.Equal(-sale.Payment.TaxAmount * ratio, note.TaxAmount);
    }

    [Fact]
    public async Task RefundPaymentAsync_WhenGatewayFails_CreatesNoFiscalDocument()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var card = new Mock<ICardPaymentService>();
        card.Setup(c => c.RefundForFiscalPaymentAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, "GATEWAY_REFUND_FAILED", "acquirer declined"));
        var payments = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Card = card.Object });
        var sale = await payments.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);
        var invoicesBefore = await ctx.Invoices.CountAsync();

        var refund = await payments.RefundPaymentAsync(
            sale.Payment!.Id,
            sale.Payment.TotalAmount,
            "Gateway abgelehnt",
            PaymentServiceCoverageHarness.CashierId,
            reasonCode: RefundReasonCode.CustomerComplaint);

        Assert.False(refund.Success);
        Assert.Equal("GATEWAY_REFUND_FAILED", refund.DiagnosticCode);
        Assert.Equal(invoicesBefore, await ctx.Invoices.CountAsync());
        Assert.Equal(0, await ctx.Invoices.CountAsync(i => i.DocumentType == DocumentType.CreditNote));
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync(p => p.IsRefund));
        Assert.Equal(0, await ctx.Receipts.CountAsync(r => r.PaymentId != sale.Payment.Id));
    }
}

public sealed class PaymentBenefitServiceTests
{
    [Fact]
    public async Task CalculateBenefitsAsync_WhenNoEligibleBenefits_ReturnsEmptySnapshot()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, _, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var customer = await ctx.Customers.FirstAsync(c => c.Id == customerId);
        var sut = CreateBenefitService(ctx);

        var result = await sut.CalculateBenefitsAsync(
            customer,
            [new PaymentItem { ProductId = productId, Quantity = 1, UnitPrice = 10m, TotalPrice = 10m }],
            new Dictionary<Guid, Guid>(),
            initialTotalAmount: 10m,
            initialTotalTaxAmount: 0.91m,
            new Dictionary<string, decimal> { ["10"] = 0.91m });

        Assert.Equal(10m, result.TotalAmount);
        Assert.Equal(0.91m, result.TotalTaxAmount);
        Assert.Null(result.AppliedBenefitsSnapshot);
        Assert.Empty(result.UsageDeltas);
    }

    [Fact]
    public async Task ApplyBenefitUsageMutationsAsync_WhenDailyLimitReached_DoesNotConsumeMore()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, _, categoryId) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var def = new BenefitDefinition
        {
            Code = "FREE-1",
            Name = "Free Speisen",
            BenefitKind = AppliedBenefitKind.FreeAllowance,
            AllowanceQuantity = 1,
            AllowanceScope = "per_day",
            AllowanceCategoryId = categoryId
        };
        ctx.BenefitDefinitions.Add(def);
        ctx.BenefitAssignments.Add(new BenefitAssignment
        {
            BenefitDefinitionId = def.Id,
            CustomerId = customerId,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            Priority = 1
        });
        ctx.BenefitDailyUsages.Add(new BenefitDailyUsage
        {
            CustomerId = customerId,
            BenefitDefinitionId = def.Id,
            UsageDate = DateTime.UtcNow.Date,
            QuantityUsed = 1
        });
        await ctx.SaveChangesAsync();
        var customer = await ctx.Customers.FirstAsync(c => c.Id == customerId);
        var sut = CreateBenefitService(ctx);

        var result = await sut.CalculateBenefitsAsync(
            customer,
            [new PaymentItem { ProductId = productId, Quantity = 1, UnitPrice = 10m, TotalPrice = 10m }],
            new Dictionary<Guid, Guid> { [productId] = categoryId },
            initialTotalAmount: 10m,
            initialTotalTaxAmount: 0.91m,
            new Dictionary<string, decimal> { ["10"] = 0.91m });
        await sut.ApplyBenefitUsageMutationsAsync(result.UsageDeltas);
        await ctx.SaveChangesAsync();

        Assert.Equal(10m, result.TotalAmount);
        Assert.Empty(result.UsageDeltas);
        var usage = await ctx.BenefitDailyUsages.AsNoTracking().SingleAsync();
        Assert.Equal(1, usage.QuantityUsed);
    }

    private static PaymentBenefitService CreateBenefitService(AppDbContext ctx) =>
        new(
            ctx,
            new GenericRepository<Customer>(ctx, NullLogger<GenericRepository<Customer>>.Instance),
            TenantTestDoubles.PrimaryTenantResolver,
            Mock.Of<ICountryStrategyContext>(),
            Mock.Of<ITaxStrategyResolver>(),
            Mock.Of<IPricingRuleResolver>(),
            Mock.Of<ICountryTaxTypeRegistry>(),
            chMwstRates: null);
}

public sealed class PaymentOfflineIntentServiceTests
{
    [Fact]
    public async Task TryQueueServerNonFiscalOfflineAsync_WhenVoucherPayment_Throws()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (_, _, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = CreateOfflineService(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.TryQueueServerNonFiscalOfflineAsync(
                new CreatePaymentRequest
                {
                    Payment = new PaymentMethodRequest { Method = "voucher", VoucherCode = "GUT-1" }
                },
                PaymentServiceCoverageHarness.CashierId,
                registerId));

        Assert.Equal("Voucher payments cannot be processed offline.", ex.Message);
        Assert.Equal(0, await ctx.OfflineTransactions.CountAsync());
    }

    [Fact]
    public async Task TryQueueServerNonFiscalOfflineAsync_WhenCashPayment_Queues()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (_, _, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = CreateOfflineService(ctx);

        var result = await sut.TryQueueServerNonFiscalOfflineAsync(
            new CreatePaymentRequest
            {
                Payment = new PaymentMethodRequest { Method = "cash" },
                TotalAmount = 10m
            },
            PaymentServiceCoverageHarness.CashierId,
            registerId);

        Assert.True(result.Success, result.Message);
        Assert.True(result.NonFiscalOfflineQueued);
        Assert.Equal("NON_FISCAL_QUEUED", result.DiagnosticCode);
        var row = await ctx.OfflineTransactions.AsNoTracking().SingleAsync();
        Assert.Equal(row.Id, result.OfflineTransactionId);
        Assert.Equal(OfflineTransactionStatus.NonFiscalPending, row.Status);
        Assert.Equal(registerId, row.CashRegisterId);
    }

    private static PaymentOfflineIntentService CreateOfflineService(AppDbContext ctx) =>
        new(
            ctx,
            NullLogger.Instance,
            TenantTestDoubles.PrimaryTenantResolver,
            new EphemeralDataProtectionProvider(),
            Options.Create(new OfflineVoucherEncryptionOptions()),
            new TseOptions(),
            hostEnvironment: null,
            developmentModeService: null,
            licenseOptions: new LicenseOptions { Enabled = false },
            tenantLimitGuard: null);
}
