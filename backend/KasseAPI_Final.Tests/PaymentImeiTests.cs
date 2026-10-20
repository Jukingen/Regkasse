using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Imei;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
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
        await SeedHandyShopAsync(ctx);

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options { VerticalProfiles = HandyShopProfiles(ctx) });
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

    private static async Task SeedHandyShopAsync(AppDbContext ctx)
    {
        if (!await ctx.VerticalProfiles.AnyAsync(row => row.Id == VerticalProfileIds.HandyShop))
        {
            ctx.VerticalProfiles.Add(new VerticalProfile
            {
                Id = VerticalProfileIds.HandyShop,
                Name = "verticalProfiles.handyShop.name",
                PosFeatures =
                    """{"tables":false,"kitchenDisplay":false,"patientRecord":false,"serviceDuration":false,"appointment":false,"imeiTracking":true,"routeTracking":false,"roomTracking":false,"ticketScan":false}""",
                RequiredFields = """{"customer":[],"product":[]}""",
                OptionalFields = """{"customer":[],"product":[]}""",
                PosLayout = VerticalProfileLayouts.Standard,
                IsActive = true,
            });
        }

        var settings = await ctx.CompanySettings
            .FirstOrDefaultAsync(row => row.TenantId == SystemTenantIds.Platform);
        if (settings is null)
        {
            ctx.CompanySettings.Add(new CompanySettings
            {
                TenantId = SystemTenantIds.Platform,
                CompanyName = "Handy Shop",
                CompanyAddress = "Wien 1",
                CompanyTaxNumber = "ATU12345678",
                BusinessHours = new Dictionary<string, string>(),
                Currency = "EUR",
                Country = "AT",
                Language = "de-DE",
                TimeZone = "Europe/Vienna",
                DateFormat = "dd.MM.yyyy",
                TimeFormat = "HH:mm",
                TaxCalculationMethod = "Standard",
                InvoiceNumbering = "Sequential",
                ReceiptNumbering = "Sequential",
                DefaultPaymentMethod = "Cash",
                VerticalProfileId = VerticalProfileIds.HandyShop,
            });
        }
        else
        {
            settings.VerticalProfileId = VerticalProfileIds.HandyShop;
        }

        await ctx.SaveChangesAsync();
    }

    private static IVerticalProfileService HandyShopProfiles(AppDbContext ctx)
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(service => service.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new VerticalProfileService(
            ctx,
            TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform),
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILogger<VerticalProfileService>>(),
            new VerticalProfileRegistry(ctx),
            cache.Object);
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
