using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Services.Vouchers;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Coverage for CreatePaymentCoreAsync: NTP, idempotency, card, license, payment methods.
/// ModifierIds on the request are unused for new writes (add-ons are separate product lines).
/// </summary>
public sealed class PaymentServiceCreatePaymentCoreCoverageTests
{
    [Fact]
    public async Task CreatePayment_WhenNtpSyncFails_BlocksFiscalPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var ntp = new Mock<INtpTimeSyncStatus>();
        string? clockMsg = "Systemzeit nicht synchronisiert";
        ntp.Setup(n => n.ShouldAllowOnlineFiscalPayment(It.IsAny<NtpSettings>(), out clockMsg))
            .Returns(false);

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                Ntp = ntp.Object,
                NtpSettings = new NtpSettings { Enabled = true }
            });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("NTP_TIME_SYNC", result.DiagnosticCode);
        Assert.True(result.IsDeterministicFailure);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenNtpSyncPasses_AllowsPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var ntp = new Mock<INtpTimeSyncStatus>();
        string? clockMsg = null;
        ntp.Setup(n => n.ShouldAllowOnlineFiscalPayment(It.IsAny<NtpSettings>(), out clockMsg))
            .Returns(true);

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                Ntp = ntp.Object,
                NtpSettings = new NtpSettings { Enabled = true }
            });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        ntp.Verify(n => n.ShouldAllowOnlineFiscalPayment(It.IsAny<NtpSettings>(), out clockMsg), Times.Once);
    }

    [Fact]
    public async Task CreatePayment_WhenIdempotencyKeyReused_ReturnsExistingPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var key = Guid.NewGuid().ToString("N");
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, idempotencyKey: key);

        var first = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);
        var second = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(first.Success, first.Message);
        Assert.True(second.Success, second.Message);
        Assert.True(second.IdempotentReplay);
        Assert.Equal(first.Payment!.Id, second.Payment!.Id);
        Assert.Equal(1, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_Card_WhenIntentValid_SucceedsAndLinks()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var intentId = Guid.NewGuid();
        var card = new Mock<ICardPaymentService>();
        card.Setup(c => c.ValidateForFiscalPaymentAsync(
                intentId, It.IsAny<decimal>(), registerId, It.IsAny<CancellationToken>()))
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

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Card = card.Object });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(
                customerId, productId, registerId, method: "card", cardPaymentIntentId: intentId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal("1", result.Payment!.PaymentMethodRaw);
        card.Verify(c => c.LinkToPaymentAsync(intentId, result.Payment.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePayment_Card_WhenIntentInvalid_ReturnsCardIntentInvalid()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var intentId = Guid.NewGuid();
        var card = new Mock<ICardPaymentService>();
        card.Setup(c => c.ValidateForFiscalPaymentAsync(
                intentId, It.IsAny<decimal>(), registerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (CardPaymentTransaction?)null, "CARD_INTENT_INVALID", "Card declined"));

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Card = card.Object });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(
                customerId, productId, registerId, method: "card", cardPaymentIntentId: intentId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("CARD_INTENT_INVALID", result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_Card_WhenIntentMissing_ReturnsCardIntentRequired()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var card = new Mock<ICardPaymentService>();
        card.Setup(c => c.ValidateForFiscalPaymentAsync(
                Guid.Empty, It.IsAny<decimal>(), registerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (CardPaymentTransaction?)null, "CARD_INTENT_REQUIRED", "Card payment requires a confirmed card payment intent."));

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Card = card.Object });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, method: "card"),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("CARD_INTENT_REQUIRED", result.DiagnosticCode);
    }

    [Fact]
    public async Task CreatePayment_WhenCardPaymentTimesOut_RollsBack()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var intentId = Guid.NewGuid();
        var card = new Mock<ICardPaymentService>();
        card.Setup(c => c.ValidateForFiscalPaymentAsync(
                intentId, It.IsAny<decimal>(), registerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Card gateway timeout"));

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Card = card.Object });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(
                customerId, productId, registerId, method: "card", cardPaymentIntentId: intentId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("An error occurred while creating payment", result.Message);
        Assert.Contains("Card gateway timeout", result.Errors);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
        Assert.Equal(0, await ctx.Invoices.CountAsync());
        Assert.Equal(0, await ctx.Receipts.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenLicenseCheckFails_ThrowsLicenseExpiredException()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await PaymentServiceCoverageHarness.SetTenantLicenseValidUntilAsync(ctx, DateTime.UtcNow.AddDays(-10));
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            PaymentServiceCoverageHarness.EnforcedLicenseOptions(
                PaymentServiceCoverageHarness.CreateLicenseService(PaymentServiceCoverageHarness.ValidDeployment()).Object));

        await Assert.ThrowsAsync<LicenseExpiredException>(() =>
            sut.CreatePaymentAsync(
                PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
                PaymentServiceCoverageHarness.CashierId));
    }

    [Fact]
    public async Task CreatePayment_WithObsoleteModifierIds_DoesNotFail()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
#pragma warning disable CS0618
        request.Items[0].ModifierIds.Add(Guid.NewGuid());
#pragma warning restore CS0618

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.NotNull(result.Payment);
    }

    [Theory]
    [InlineData("cash", "0")]
    [InlineData("banktransfer", "2")]
    [InlineData("mobile", "5")]
    public async Task CreatePayment_WithSupportedMethods_Succeeds(string method, string expectedLegacy)
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, method: method),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(expectedLegacy, result.Payment!.PaymentMethodRaw);
    }

    [Fact]
    public async Task CreatePayment_WhenFONSubmitFails_RetriesSuccessfully()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var finanz = new Mock<IFinanzOnlineService>();
        finanz.SetupSequence(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ReturnsAsync(new FinanzOnlineSubmitResponse
            {
                Success = false,
                ErrorMessage = "FON timeout",
                Status = "Failed",
                SubmittedAt = DateTime.UtcNow,
                FailureKind = FinanzOnlineFailureKind.Transient
            })
            .ReturnsAsync(new FinanzOnlineSubmitResponse
            {
                Success = true,
                ReferenceId = "FON-OK-1",
                Status = "Submitted",
                SubmittedAt = DateTime.UtcNow,
                FailureKind = FinanzOnlineFailureKind.None
            });

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Finanz = finanz });

        var created = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(created.Success, created.Message);

        var afterCreate = await ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == created.Payment!.Id);
        Assert.Equal("Pending", afterCreate.FinanzOnlineStatus);
        Assert.Equal(0, afterCreate.FinanzOnlineRetryCount);

        var retry = await sut.RetryFinanzOnlineSubmitAsync(created.Payment!.Id);
        Assert.True(retry.Success, retry.ErrorMessage);
        Assert.Equal("FON-OK-1", retry.ReferenceId);

        var afterRetry = await ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == created.Payment.Id);
        Assert.Equal("Submitted", afterRetry.FinanzOnlineStatus);
        Assert.Equal(1, afterRetry.FinanzOnlineRetryCount);
        Assert.Equal("FON-OK-1", afterRetry.FinanzOnlineReferenceId);
        Assert.Equal(1, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenFONSubmitFails_AllRetriesExhausted()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var finanz = new Mock<IFinanzOnlineService>();
        finanz.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ReturnsAsync(new FinanzOnlineSubmitResponse
            {
                Success = false,
                ErrorMessage = "FON permanently rejected",
                Status = "Failed",
                SubmittedAt = DateTime.UtcNow,
                FailureKind = FinanzOnlineFailureKind.Permanent
            });

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Finanz = finanz });

        var created = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(created.Success, created.Message);

        var firstRetry = await sut.RetryFinanzOnlineSubmitAsync(created.Payment!.Id);
        var secondRetry = await sut.RetryFinanzOnlineSubmitAsync(created.Payment.Id);

        Assert.False(firstRetry.Success);
        Assert.False(secondRetry.Success);
        var stored = await ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == created.Payment.Id);
        Assert.Equal("Failed", stored.FinanzOnlineStatus);
        Assert.Equal(2, stored.FinanzOnlineRetryCount);
        Assert.Equal(1, await ctx.PaymentDetails.CountAsync());
        Assert.Equal(1, await ctx.Invoices.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenRegisterDecommissioned_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var register = await ctx.CashRegisters.FirstAsync(r => r.Id == registerId);
        register.Status = RegisterStatus.Decommissioned;
        await ctx.SaveChangesAsync();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(RksvGuardErrorCodes.RegisterDecommissioned, result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenRegisterClosed_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var register = await ctx.CashRegisters.FirstAsync(r => r.Id == registerId);
        register.Status = RegisterStatus.Closed;
        await ctx.SaveChangesAsync();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(CashRegisterResolutionCodes.Closed, result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenCashRegisterIdMissing_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, _, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, Guid.Empty);

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(CashRegisterResolutionCodes.Required, result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WithUnknownTaxType_UsesStandardRateFallback()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, _, registerId, categoryId) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var unknownTaxProductId = await PaymentServiceCoverageHarness.AddProductAsync(
            ctx, categoryId, "Unknown VAT", 12m, taxType: 99);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, unknownTaxProductId, registerId, total: 12m),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        var expected = CartMoneyHelper.ComputeLine(12m, 1, 99);
        Assert.Equal(12m, result.Payment!.TotalAmount);
        Assert.Equal(expected.LineTax, result.Payment.TaxAmount);
        Assert.Equal(0.20m, expected.TaxRate);
    }

    [Fact]
    public async Task CreatePayment_WithMultipleTaxRates_CalculatesCorrectly()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, reducedProductId, registerId, categoryId) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, unitPrice: 10m);
        var standardProductId = await PaymentServiceCoverageHarness.AddProductAsync(
            ctx, categoryId, "Wein", 10m, TaxTypes.Standard);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var reduced = CartMoneyHelper.ComputeLine(10m, 1, TaxTypes.Reduced);
        var standard = CartMoneyHelper.ComputeLine(10m, 1, TaxTypes.Standard);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, reducedProductId, registerId, total: 20m);
        request.Items =
        [
            new PaymentItemRequest { ProductId = reducedProductId, Quantity = 1, TaxType = TaxType.Reduced },
            new PaymentItemRequest { ProductId = standardProductId, Quantity = 1, TaxType = TaxType.Standard }
        ];

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(20m, result.Payment!.TotalAmount);
        Assert.Equal(reduced.LineTax + standard.LineTax, result.Payment.TaxAmount);

        var taxDetails = JsonSerializer.Deserialize<Dictionary<string, decimal>>(
            result.Payment.TaxDetails.RootElement.GetRawText());
        Assert.NotNull(taxDetails);
        Assert.Equal(reduced.LineTax, taxDetails!["2"]);
        Assert.Equal(standard.LineTax, taxDetails["1"]);
    }

    [Fact]
    public async Task CreatePayment_ConcurrentRequests_HandlesRaceConditions()
    {
        var dbName = $"PayRace_{Guid.NewGuid():N}";
        await using var ctxA = PaymentServiceCoverageHarness.CreateContext(dbName);
        await using var ctxB = PaymentServiceCoverageHarness.CreateContext(dbName);
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctxA);
        var seq = 0;
        var receiptSeq = new Mock<IReceiptSequenceService>();
        receiptSeq.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) =>
                $"AT-{reg}-{d:yyyyMMdd}-{Interlocked.Increment(ref seq)}");

        var sutA = PaymentServiceCoverageHarness.CreatePaymentService(
            ctxA,
            new PaymentServiceCoverageHarness.Options { ReceiptSeq = receiptSeq });
        var sutB = PaymentServiceCoverageHarness.CreatePaymentService(
            ctxB,
            new PaymentServiceCoverageHarness.Options { ReceiptSeq = receiptSeq });

        var keyA = Guid.NewGuid().ToString("N");
        var keyB = Guid.NewGuid().ToString("N");
        var requestA = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, idempotencyKey: keyA);
        var requestB = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, idempotencyKey: keyB);

        var results = await Task.WhenAll(
            sutA.CreatePaymentAsync(requestA, PaymentServiceCoverageHarness.CashierId),
            sutB.CreatePaymentAsync(requestB, PaymentServiceCoverageHarness.CashierId));

        Assert.True(results[0].Success, results[0].Message);
        Assert.True(results[1].Success, results[1].Message);
        Assert.NotEqual(results[0].Payment!.Id, results[1].Payment!.Id);

        await using var verify = PaymentServiceCoverageHarness.CreateContext(dbName);
        Assert.Equal(2, await verify.PaymentDetails.CountAsync());
        Assert.Equal(2, await verify.Invoices.CountAsync());
        Assert.Equal(2, await verify.Receipts.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenTseSigningFails_RollsBackCompletely()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var tse = PaymentServiceCoverageHarness.CreateTseMock();
        tse.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()))
            .ThrowsAsync(new InvalidOperationException("TSE signing aborted"));
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { TseMock = tse });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("Failed to generate TSE signature", result.Message);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
        Assert.Equal(0, await ctx.Invoices.CountAsync());
        Assert.Equal(0, await ctx.Receipts.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenProductMissing_RollsBackWithoutPartialData()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, _, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, Guid.NewGuid(), registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("Product not found", result.Message);
        Assert.True(result.IsDeterministicFailure);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
        Assert.Equal(0, await ctx.Invoices.CountAsync());
        Assert.Equal(0, await ctx.Receipts.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WithMultipleVouchers_AppliesAllCorrectly()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, unitPrice: 10m);
        var voucherA = await AddVoucherAsync(ctx, "GUT-MIX-A", 100m);
        var voucherB = await AddVoucherAsync(ctx, "GUT-MIX-B", 50m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 10m, method: "voucher");
        request.Payment.VoucherRedemptions =
        [
            new VoucherRedemptionRequestItem { Code = "GUT-MIX-A", Amount = 6m },
            new VoucherRedemptionRequestItem { Code = "GUT-MIX-B", Amount = 4m }
        ];

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        var a = await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherA);
        var b = await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherB);
        Assert.Equal(94m, a.RemainingAmount);
        Assert.Equal(46m, b.RemainingAmount);
        Assert.Equal(2, await ctx.VoucherLedgerEntries.CountAsync(
            l => l.PaymentId == result.Payment!.Id && l.Type == VoucherTransactionType.Redeem));
    }

    [Fact]
    public async Task CreatePayment_WithVoucherAndCash_MixedPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, unitPrice: 10m);
        var voucherId = await AddVoucherAsync(ctx, "GUT-CASH-1", 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 10m, method: "cash");
        request.Payment.Amount = 4m;
        request.Payment.VoucherRedemptions =
        [
            new VoucherRedemptionRequestItem { Code = "GUT-CASH-1", Amount = 6m }
        ];

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal("0", result.Payment!.PaymentMethodRaw);
        var voucher = await ctx.Vouchers.AsNoTracking().FirstAsync(v => v.Id == voucherId);
        Assert.Equal(94m, voucher.RemainingAmount);
        var redeem = await ctx.VoucherLedgerEntries.AsNoTracking()
            .SingleAsync(l => l.PaymentId == result.Payment.Id && l.Type == VoucherTransactionType.Redeem);
        Assert.Equal(-6m, redeem.Amount);
    }

    [Fact]
    public async Task CreatePayment_WithVoucherAndCash_MissingSettlementAmount_Blocks()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, unitPrice: 10m);
        await AddVoucherAsync(ctx, "GUT-CASH-2", 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 10m, method: "cash");
        request.Payment.VoucherRedemptions =
        [
            new VoucherRedemptionRequestItem { Code = "GUT-CASH-2", Amount = 6m }
        ];

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("VOUCHER_MIXED_SETTLEMENT_AMOUNT_REQUIRED", result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WithReservedReceiptNumber_UsesReservedNumber()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var receiptSeq = new Mock<IReceiptSequenceService>();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { ReceiptSeq = receiptSeq });
        var reserved = "AT-KASSE-01-RESERVED-1";
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.ReservedReceiptNumber = reserved;

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(reserved, result.Payment!.ReceiptNumber);
        receiptSeq.Verify(
            x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task CreatePayment_WithReservedNumberAlreadyUsed_ReturnsConflict()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var first = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(first.Success, first.Message);

        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.ReservedReceiptNumber = first.Payment!.ReceiptNumber;

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("RECEIPT_NUMBER_CONFLICT", result.DiagnosticCode);
        Assert.Equal("Reserved receipt number is already in use.", result.Message);
        Assert.Equal(1, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenStockEnforced_AndInsufficientStock_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var product = await ctx.Products.FirstAsync(p => p.Id == productId);
        product.StockQuantity = 0;
        await ctx.SaveChangesAsync();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                Inventory = new InventoryOptions { EnforceStockOnSales = true }
            });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("Insufficient stock", result.Message);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
        Assert.Equal(0, (await ctx.Products.AsNoTracking().FirstAsync(p => p.Id == productId)).StockQuantity);
    }

    [Fact]
    public async Task CreatePayment_WhenStockEnforced_AndStockAvailable_AllowsPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                Inventory = new InventoryOptions { EnforceStockOnSales = true }
            });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(99, (await ctx.Products.AsNoTracking().FirstAsync(p => p.Id == productId)).StockQuantity);
    }

    [Fact]
    public async Task CreatePayment_ConcurrentRequests_WithSameIdempotencyKey_OnlyOneSucceeds()
    {
        var dbName = $"PayIdem_{Guid.NewGuid():N}";
        await using var ctxA = PaymentServiceCoverageHarness.CreateContext(dbName);
        await using var ctxB = PaymentServiceCoverageHarness.CreateContext(dbName);
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctxA);
        var key = Guid.NewGuid().ToString("N");
        var sutA = PaymentServiceCoverageHarness.CreatePaymentService(ctxA);
        var first = await sutA.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, idempotencyKey: key),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(first.Success, first.Message);

        var sutB = PaymentServiceCoverageHarness.CreatePaymentService(ctxB);
        await using var ctxC = PaymentServiceCoverageHarness.CreateContext(dbName);
        var sutC = PaymentServiceCoverageHarness.CreatePaymentService(ctxC);
        var replay = await Task.WhenAll(
            sutB.CreatePaymentAsync(
                PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, idempotencyKey: key),
                PaymentServiceCoverageHarness.CashierId),
            sutC.CreatePaymentAsync(
                PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, idempotencyKey: key),
                PaymentServiceCoverageHarness.CashierId));

        Assert.True(replay[0].Success);
        Assert.True(replay[1].Success);
        Assert.True(replay[0].IdempotentReplay);
        Assert.True(replay[1].IdempotentReplay);
        Assert.Equal(first.Payment!.Id, replay[0].Payment!.Id);
        Assert.Equal(first.Payment.Id, replay[1].Payment!.Id);
        await using var verify = PaymentServiceCoverageHarness.CreateContext(dbName);
        Assert.Equal(1, await verify.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenCustomerMissing_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (_, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(Guid.NewGuid(), productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("Customer not found", result.Message);
        Assert.True(result.IsDeterministicFailure);
    }

    [Fact]
    public async Task CreatePayment_WhenDemoUser_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { DemoUser = true });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("DEMO_BY_FLAG", result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenNewPaymentFlowFlagEnabled_StillCreatesPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.IsEnabled(FeatureFlagNames.EnableNewPaymentFlow, It.IsAny<string?>()))
            .Returns(true);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { FeatureFlags = flags.Object });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message);
        flags.Verify(f => f.IsEnabled(FeatureFlagNames.EnableNewPaymentFlow, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CreatePayment_WhenPaymentMethodDisabled_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        ctx.PaymentMethodDefinitions.Add(new PaymentMethodDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = registerId,
            Code = "cash",
            Name = "Bar",
            IsActive = false,
            LegacyPaymentMethodValue = 0,
            CreatedAtUtc = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenIsStornoAndIsRefund_BlocksAsMutuallyExclusive()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.IsStorno = true;
        request.IsRefund = true;
        request.OriginalReceiptNumber = "AT-KASSE-01-X";
        request.StornoReason = StornoReason.KundeStorniert;

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Contains("mutually exclusive", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePayment_WhenIsStorno_CreatesReversalFromOriginalReceipt()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            Items = [],
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            TableNumber = 1,
            TotalAmount = 10m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            IsStorno = true,
            OriginalReceiptNumber = sale.Payment!.ReceiptNumber,
            StornoReason = StornoReason.KundeStorniert,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.True(result.Payment!.IsStorno);
        Assert.Equal(sale.Payment.Id, result.Payment.OriginalPaymentId);
        Assert.Equal(-10m, result.Payment.TotalAmount);
    }

    [Fact]
    public async Task CreatePayment_WhenIsStorno_AndCashRegisterIdMissing_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            Items = [],
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            TableNumber = 1,
            TotalAmount = 10m,
            Steuernummer = "ATU12345678",
            CashRegisterId = Guid.Empty,
            IsStorno = true,
            OriginalReceiptNumber = sale.Payment!.ReceiptNumber,
            StornoReason = StornoReason.KundeStorniert,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("CashRegisterId is required", result.Message);
        Assert.Equal(CashRegisterResolutionCodes.Required, result.DiagnosticCode);
    }

    [Fact]
    public async Task CreatePayment_WhenIsStorno_AndRegisterClosed_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);

        var register = await ctx.CashRegisters.FirstAsync(r => r.Id == registerId);
        register.Status = RegisterStatus.Closed;
        await ctx.SaveChangesAsync();

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            Items = [],
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            TableNumber = 1,
            TotalAmount = 10m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            IsStorno = true,
            OriginalReceiptNumber = sale.Payment!.ReceiptNumber,
            StornoReason = StornoReason.KundeStorniert,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(CashRegisterResolutionCodes.Closed, result.DiagnosticCode);
    }

    [Fact]
    public async Task CreatePayment_WhenIsStorno_AndCustomerMismatch_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var otherCustomerId = Guid.NewGuid();
        ctx.Customers.Add(new Customer
        {
            Id = otherCustomerId,
            Name = "Anderer Gast",
            Email = "a@t.com",
            Phone = "2",
            IsActive = true
        });
        await ctx.SaveChangesAsync();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);

        var request = new CreatePaymentRequest
        {
            CustomerId = otherCustomerId,
            Items = [],
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            TableNumber = 1,
            TotalAmount = 10m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            IsStorno = true,
            OriginalReceiptNumber = sale.Payment!.ReceiptNumber,
            StornoReason = StornoReason.KundeStorniert,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("Customer does not match original receipt", result.Message);
    }

    [Fact]
    public async Task CreatePayment_WhenIsStorno_AndTotalMismatch_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            Items = [],
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            TableNumber = 1,
            TotalAmount = 5m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            IsStorno = true,
            OriginalReceiptNumber = sale.Payment!.ReceiptNumber,
            StornoReason = StornoReason.KundeStorniert,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("Storno total does not match original receipt", result.Message);
    }

    [Fact]
    public async Task CreatePayment_WhenIsStorno_AfterPartialRefund_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);

        var refund = await sut.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                CustomerId = customerId,
                Items = [],
                Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
                TableNumber = 1,
                TotalAmount = 4m,
                Steuernummer = "ATU12345678",
                CashRegisterId = registerId,
                IsRefund = true,
                OriginalReceiptNumber = sale.Payment!.ReceiptNumber,
                Notes = "Refund (POS create)",
                IdempotencyKey = Guid.NewGuid().ToString("N")
            },
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(refund.Success, refund.Message);

        var result = await sut.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                CustomerId = customerId,
                Items = [],
                Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
                TableNumber = 1,
                TotalAmount = 10m,
                Steuernummer = "ATU12345678",
                CashRegisterId = registerId,
                IsStorno = true,
                OriginalReceiptNumber = sale.Payment.ReceiptNumber,
                StornoReason = StornoReason.KundeStorniert,
                IdempotencyKey = Guid.NewGuid().ToString("N")
            },
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("STORNO_BLOCKED_BY_REFUNDS", result.DiagnosticCode);
    }

    [Fact]
    public async Task CreatePayment_WhenIdempotencyReplay_AttachesOfflineTransactionId()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var key = Guid.NewGuid().ToString("N");
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, idempotencyKey: key);
        var first = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);
        Assert.True(first.Success, first.Message);

        var offlineId = Guid.NewGuid();
        var replay = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId, offlineId);

        Assert.True(replay.Success);
        Assert.True(replay.IdempotentReplay);
        Assert.Equal(first.Payment!.Id, replay.Payment!.Id);
        Assert.Equal(offlineId, (await ctx.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == first.Payment.Id)).OfflineTransactionId);
    }

    [Fact]
    public async Task CreatePayment_WhenTaxNumberInvalid_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                CompanyProfile = new CompanyProfileOptions
                {
                    CompanyName = "Test GmbH",
                    TaxNumber = "INVALID",
                    Street = "S1",
                    ZipCode = "1010",
                    City = "Wien",
                    FooterText = ""
                }
            });
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.Steuernummer = "INVALID";

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("Invalid Austrian tax number format", result.Message);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenIsRefund_CreatesPartialRefundFromOriginalReceipt()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var sale = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(sale.Success, sale.Message);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            Items = [],
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            TableNumber = 1,
            TotalAmount = 4m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            IsRefund = true,
            OriginalReceiptNumber = sale.Payment!.ReceiptNumber,
            Notes = "Refund (POS create)",
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.True(result.Payment!.IsRefund);
        Assert.Equal(sale.Payment.Id, result.Payment.OriginalPaymentId);
        Assert.Equal(-4m, result.Payment.TotalAmount);
    }

    [Fact]
    public async Task CreatePayment_WhenStornoOriginalReceiptMissing_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            Items = [],
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            TableNumber = 1,
            TotalAmount = 10m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            IsStorno = true,
            OriginalReceiptNumber = "AT-KASSE-01-MISSING",
            StornoReason = StornoReason.KundeStorniert,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("Original receipt not found", result.Message);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WithVoucherAndCash_NegativeSettlement_Blocks()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, unitPrice: 10m);
        await AddVoucherAsync(ctx, "GUT-CASH-NEG", 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 10m, method: "cash");
        request.Payment.Amount = -1m;
        request.Payment.VoucherRedemptions =
        [
            new VoucherRedemptionRequestItem { Code = "GUT-CASH-NEG", Amount = 11m }
        ];

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("VOUCHER_MIXED_SETTLEMENT_NEGATIVE", result.DiagnosticCode);
    }

    [Fact]
    public async Task CreatePayment_WithVoucherAndCash_SettlementExceedsTotal_Blocks()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, unitPrice: 10m);
        await AddVoucherAsync(ctx, "GUT-CASH-EX", 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 10m, method: "cash");
        request.Payment.Amount = 11m;
        request.Payment.VoucherRedemptions =
        [
            new VoucherRedemptionRequestItem { Code = "GUT-CASH-EX", Amount = 1m }
        ];

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("VOUCHER_MIXED_SETTLEMENT_EXCEEDS_TOTAL", result.DiagnosticCode);
    }

    [Fact]
    public async Task CreatePayment_WithVoucherAndCash_ZeroRedeem_Blocks()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, unitPrice: 10m);
        await AddVoucherAsync(ctx, "GUT-CASH-ZERO", 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 10m, method: "cash");
        request.Payment.Amount = 10m;
        request.Payment.VoucherRedemptions =
        [
            new VoucherRedemptionRequestItem { Code = "GUT-CASH-ZERO", Amount = 0.01m }
        ];

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("VOUCHER_MIXED_ZERO_REDEEM", result.DiagnosticCode);
    }

    [Fact]
    public async Task CreatePayment_WhenVoucherMethodHasNoPayload_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, method: "voucher"),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(RksvGuardErrorCodes.VoucherCodeRequired, result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenVoucherCatalogLegacyMismatch_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await AddVoucherAsync(ctx, "GUT-LEGACY-1", 100m);
        ctx.PaymentMethodDefinitions.Add(new PaymentMethodDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = registerId,
            Code = "voucher",
            Name = "Gutschein",
            IsActive = true,
            LegacyPaymentMethodValue = 1,
            CreatedAtUtc = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, method: "voucher");
        request.Payment.VoucherCode = "GUT-LEGACY-1";

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("VOUCHER_LEGACY_MISMATCH", result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenVoucherRedemptionTotalMismatches_BlocksPayment()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, unitPrice: 10m);
        await AddVoucherAsync(ctx, "GUT-SUM-1", 100m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 10m, method: "voucher");
        request.Payment.VoucherRedemptions =
        [
            new VoucherRedemptionRequestItem { Code = "GUT-SUM-1", Amount = 3m }
        ];

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("VOUCHER_INVALID", result.DiagnosticCode);
        Assert.Contains("expected voucher amount", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenCartSnapshotExists_UsesSnapshotUnitPrices()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, unitPrice: 10m);
        ctx.Carts.Add(new Cart
        {
            CartId = Guid.NewGuid().ToString("N")[..12],
            TableNumber = 1,
            UserId = PaymentServiceCoverageHarness.CashierId,
            Status = CartStatus.Active,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2)
        });
        ctx.CartItems.Add(new CartItem
        {
            CartId = ctx.Carts.Local.First().CartId,
            ProductId = productId,
            Quantity = 1,
            UnitPrice = 8m
        });
        await ctx.SaveChangesAsync();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 8m);

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(8m, result.Payment!.TotalAmount);
    }

    private static async Task<Guid> AddVoucherAsync(AppDbContext ctx, string code, decimal remaining)
    {
        var voucherId = Guid.NewGuid();
        ctx.Vouchers.Add(new Voucher
        {
            Id = voucherId,
            TenantId = SystemTenantIds.Platform,
            CodeHash = VoucherCodeHasher.HashNormalized(VoucherCodeHasher.NormalizeCode(code)),
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
}
