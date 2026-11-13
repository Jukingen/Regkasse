using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Limits;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

[Collection("OpenApiExportWebHost")]
public sealed class PaymentServiceTenantLimitTests
{
    public PaymentServiceTenantLimitTests()
    {
        OpenApiExportHostGate.EnsureExportModeDisabled();
    }

    [Fact]
    public async Task CreatePayment_WhenDailyTransactionLimitExceeded_ReturnsDeterministicFailure()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);

        var guard = new Mock<ITenantLimitGuard>();
        guard
            .Setup(g => g.EnsureSaleWithinLimitsAsync(
                It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LimitExceededException(
                TenantLimitKeys.DailyMaxTransactions,
                1,
                1,
                "Daily transaction limit of 1 reached"));

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { TenantLimitGuard = guard.Object });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(LimitExceededException.ErrorCodeValue, result.DiagnosticCode);
        Assert.NotNull(result.LimitError);
        Assert.Equal(TenantLimitKeys.DailyMaxTransactions, result.LimitError.LimitKey);
        Assert.True(result.IsDeterministicFailure);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_WhenAmountExceedsCap_ReturnsAmountCode()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);

        var guard = new Mock<ITenantLimitGuard>();
        guard
            .Setup(g => g.EnsureSaleWithinLimitsAsync(
                It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LimitExceededException(
                TenantLimitKeys.MaxTransactionAmount,
                5m,
                10m,
                "Maximum transaction amount is 5"));

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { TenantLimitGuard = guard.Object });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(LimitExceededException.ErrorCodeValue, result.DiagnosticCode);
        Assert.NotNull(result.LimitError);
        Assert.Equal(TenantLimitKeys.MaxTransactionAmount, result.LimitError.LimitKey);
        Assert.True(result.IsDeterministicFailure);
    }
}
