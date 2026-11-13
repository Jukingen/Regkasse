using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentVariantTests
{
    [Fact]
    public async Task CreatePayment_RequiresVariant_WhenProductHasActiveVariants()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        AddVariant(ctx, productId, "SKU-RED", "Rot", 0m, 4);
        await ctx.SaveChangesAsync();

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(ProductVariantSaleCodes.Required, result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_RejectsVariant_ForDifferentProduct()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        AddVariant(ctx, productId, "SKU-RED", "Rot", 0m, 4);
        var foreign = AddVariant(ctx, Guid.NewGuid(), "SKU-OTHER", "Blau", 0m, 4);
        await ctx.SaveChangesAsync();

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.Items[0].VariantId = foreign.Id;

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(ProductVariantSaleCodes.Invalid, result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_RejectsVariant_WhenProductHasNoVariants()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.Items[0].VariantId = Guid.NewGuid();

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(ProductVariantSaleCodes.NotAllowed, result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task CreatePayment_Rejects_WhenVariantStockIsInsufficient()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var variant = AddVariant(ctx, productId, "SKU-RED", "Rot", priceDelta: 0m, stock: 0);
        await ctx.SaveChangesAsync();

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.Items[0].VariantId = variant.Id;

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(ProductVariantSaleCodes.StockInsufficient, result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
        var stored = await ctx.ProductVariants.AsNoTracking().SingleAsync();
        Assert.Equal(0, stored.Stock);
    }

    [Fact]
    public async Task CreatePayment_RollsBackPayment_WhenVariantStockDecrementCannotCommit()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var variant = AddVariant(ctx, productId, "SKU-RED", "Rot", priceDelta: 5m, stock: 3);
        await ctx.SaveChangesAsync();

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                Inventory = new InventoryOptions { EnforceStockOnSales = true },
            });
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.Items[0].VariantId = variant.Id;

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
        var stored = await ctx.ProductVariants.AsNoTracking().SingleAsync(row => row.Id == variant.Id);
        Assert.Equal(3, stored.Stock);
        var product = await ctx.Products.AsNoTracking().SingleAsync(row => row.Id == productId);
        Assert.Equal(100, product.StockQuantity);
    }

    [Fact]
    public async Task CreatePayment_DecrementsVariantStock_AndLeavesReceiptShapeUnchanged()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var plain = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        Assert.True(plain.Success, plain.Message + ": " + string.Join("; ", plain.Errors));

        var variant = AddVariant(ctx, productId, "SKU-RED", "Rot", priceDelta: 0m, stock: 4);
        await ctx.SaveChangesAsync();
        var withVariant = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        withVariant.Items[0].VariantId = variant.Id;
        var variantSale = await sut.CreatePaymentAsync(withVariant, PaymentServiceCoverageHarness.CashierId);
        Assert.True(variantSale.Success, variantSale.Message + ": " + string.Join("; ", variantSale.Errors));

        var stored = await ctx.ProductVariants.AsNoTracking().SingleAsync(row => row.Id == variant.Id);
        Assert.Equal(3, stored.Stock);
        Assert.Equal(2, await ctx.PaymentDetails.CountAsync());

        var receipts = await ctx.Receipts.AsNoTracking().OrderBy(row => row.CreatedAt).ToListAsync();
        Assert.Equal(2, receipts.Count);
        Assert.Equal(receipts[0].SubTotal, receipts[1].SubTotal);
        Assert.Equal(receipts[0].TaxTotal, receipts[1].TaxTotal);
        Assert.Equal(receipts[0].GrandTotal, receipts[1].GrandTotal);
        Assert.False(string.IsNullOrWhiteSpace(receipts[0].ReceiptNumber));
        Assert.False(string.IsNullOrWhiteSpace(receipts[1].ReceiptNumber));
        Assert.DoesNotContain("Variant", typeof(PaymentDetails).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain("Variant", typeof(Receipt).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain(variant.Id.ToString(), receipts[1].QrCodePayload ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static ProductVariant AddVariant(
        AppDbContext ctx,
        Guid productId,
        string sku,
        string label,
        decimal priceDelta,
        int stock)
    {
        var row = new ProductVariant
        {
            Id = Guid.NewGuid(),
            TenantId = SystemTenantIds.Platform,
            ProductId = productId,
            Sku = sku,
            VariantLabel = label,
            PriceDelta = priceDelta,
            Stock = stock,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        ctx.ProductVariants.Add(row);
        return row;
    }
}
