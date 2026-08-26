using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Gap coverage for mixed remainder, webhook isolation, and gateway refund-before-fiscal.
/// </summary>
public sealed class OnlinePaymentGapTests
{
    [Fact]
    public async Task CreatePayment_Card_WhenMixedVoucher_ValidatesRemainderNotGross()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, unitPrice: 10m);
        await PaymentServiceCoverageHarness.AddVoucherAsync(ctx, "GUT-GAP-1", 100m);
        var intentId = Guid.NewGuid();
        const decimal remainder = 4m;
        const decimal gross = 10m;
        var card = new Mock<ICardPaymentService>();
        card.Setup(c => c.ValidateForFiscalPaymentAsync(
                intentId, remainder, registerId, It.IsAny<CancellationToken>()))
.ReturnsAsync((true, new CardPaymentTransaction
            {
                Id = intentId,
                TenantId = SystemTenantIds.Platform,
                Amount = remainder,
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

        var request = PaymentServiceCoverageHarness.SaleRequest(
            customerId, productId, registerId, total: gross, method: "card", cardPaymentIntentId: intentId);
        request.Payment.Amount = remainder;
        request.Payment.VoucherRedemptions =
        [
            new VoucherRedemptionRequestItem { Code = "GUT-GAP-1", Amount = 6m }
        ];

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        card.Verify(
            c => c.ValidateForFiscalPaymentAsync(intentId, remainder, registerId, It.IsAny<CancellationToken>()),
            Times.Once);
        card.Verify(
            c => c.ValidateForFiscalPaymentAsync(intentId, gross, registerId, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WebhookSuccess_DoesNotInsertPaymentDetails()
    {
        var (sut, _, _, ctx) = CreateGatewaySut();
        var row = new CardPaymentTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = Guid.NewGuid(),
            Amount = 10m,
            Currency = "EUR",
            Gateway = "Mock",
            MethodCode = OnlinePaymentMethods.Card,
            GatewayPaymentIntentId = "pi_gap",
            Status = CardPaymentTransactionStatuses.Pending,
            CreatedAt = DateTime.UtcNow
        };
        ctx.CardPaymentTransactions.Add(row);
        await ctx.SaveChangesAsync();

        var applied = await sut.VerifyWebhookAsync(
            "mock",
            """{"paymentIntentId":"pi_gap","status":"succeeded","eventId":"evt-gap"}""",
            new Microsoft.AspNetCore.Http.HeaderDictionary());

        Assert.True(applied.Applied);
        Assert.Equal(OnlinePaymentStatuses.GatewaySucceeded, applied.Payment!.Status);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
        Assert.Null((await ctx.CardPaymentTransactions.AsNoTracking().SingleAsync(c => c.Id == row.Id)).PaymentId);
    }

    [Fact]
    public async Task CancelPayment_CallsGatewayRefundBeforeFiscalRow()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, unitPrice: 10m);
        var intentId = Guid.NewGuid();
        var card = new Mock<ICardPaymentService>();
        card.Setup(c => c.ValidateForFiscalPaymentAsync(
                intentId, 10m, registerId, It.IsAny<CancellationToken>()))
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
        card.Setup(c => c.RefundForFiscalPaymentAsync(It.IsAny<Guid>(), 10m, It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (string?)null, (string?)null));

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { Card = card.Object });

        var request = PaymentServiceCoverageHarness.SaleRequest(
            customerId, productId, registerId, total: 10m, method: "card", cardPaymentIntentId: intentId);
        var created = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);
        Assert.True(created.Success, created.Message + ": " + string.Join("; ", created.Errors));
        Assert.NotNull(created.Payment);

        var storno = await sut.CancelPaymentAsync(
            created.Payment!.Id,
            "Kunde hat storniert",
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(storno.Success, storno.Message + ": " + string.Join("; ", storno.Errors));
        card.Verify(
            c => c.RefundForFiscalPaymentAsync(created.Payment.Id, 10m, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static (PaymentGatewayService Sut, Mock<KasseAPI_Final.Services.PaymentGateway.IPaymentGateway> Gateway, Mock<ICashRegisterResolutionService> Resolution, Data.AppDbContext Ctx)
        CreateGatewaySut()
    {
        var ctx = PaymentServiceCoverageHarness.CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        ctx.SaveChanges();
        var gateway = new Mock<KasseAPI_Final.Services.PaymentGateway.IPaymentGateway>();
        gateway.SetupGet(g => g.ProviderName).Returns("Mock");
        var resolution = new Mock<ICashRegisterResolutionService>();
        var http = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        http.Setup(h => h.HttpContext).Returns(new Microsoft.AspNetCore.Http.DefaultHttpContext());
        var sut = new PaymentGatewayService(
            ctx,
            gateway.Object,
            resolution.Object,
            TenantTestDoubles.PrimaryTenantResolver,
            Microsoft.Extensions.Options.Options.Create(new KasseAPI_Final.Configuration.PaymentGatewayOptions()),
            http.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentGatewayService>.Instance);
        return (sut, gateway, resolution, ctx);
    }
}
