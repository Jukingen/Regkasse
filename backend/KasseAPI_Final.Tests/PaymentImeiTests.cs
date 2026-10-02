using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Imei;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentImeiTests
{
    [Fact]
    public async Task CreatePayment_MarksImeiSold_WithSoldPaymentId()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);

        var product = await ctx.Products.SingleAsync(row => row.Id == productId);
        product.ImeiTracked = true;
        ctx.ProductImeis.Add(new ProductImei
        {
            Id = Guid.NewGuid(),
            TenantId = SystemTenantIds.Platform,
            ProductId = productId,
            Imei = "490154203237518",
            Status = ProductImeiStatus.InStock,
            WarrantyMonths = 12,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.Items[0].Imei = "490154203237518";

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        var payment = await ctx.PaymentDetails.AsNoTracking().SingleAsync();
        var imei = await ctx.ProductImeis.AsNoTracking().SingleAsync();
        Assert.Equal(ProductImeiStatus.Sold, imei.Status);
        Assert.Equal(payment.Id, imei.SoldPaymentId);
        Assert.NotNull(imei.SoldAtUtc);
    }

    [Fact]
    public async Task CreatePayment_RejectsTrackedProduct_WithoutImei()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);

        var product = await ctx.Products.SingleAsync(row => row.Id == productId);
        product.ImeiTracked = true;
        await ctx.SaveChangesAsync();

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(ProductImeiErrorCodes.Required, result.DiagnosticCode);
        Assert.True(result.IsDeterministicFailure);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }
}
