using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ReceiptServiceGetReceiptSplitQueryTests
{
    [Fact]
    public async Task GetReceiptAsync_SplitQuery_ReturnsItemsAndTaxLines()
    {
        var tenantId = SystemTenantIds.Platform;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptSplit_{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        TenantTestDoubles.EnsurePlatformTenant(db);

        var registerId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Wien",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
        });
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CashRegisterId = registerId,
            CustomerName = "Gast",
            CashierId = "cashier",
            TotalAmount = 12m,
            TaxAmount = 2m,
            Steuernummer = "ATU12345678",
            TseTimestamp = DateTime.UtcNow,
            TseSignature = "sig",
            ReceiptNumber = "R-1",
            CreatedAt = DateTime.UtcNow,
        });
        db.Receipts.Add(new Receipt
        {
            ReceiptId = receiptId,
            TenantId = tenantId,
            PaymentId = paymentId,
            ReceiptNumber = "R-1",
            IssuedAt = DateTime.UtcNow,
            CashRegisterId = registerId,
            SubTotal = 10m,
            TaxTotal = 2m,
            GrandTotal = 12m,
            Items =
            [
                new ReceiptItem
                {
                    TenantId = tenantId,
                    ReceiptId = receiptId,
                    ProductName = "Kaffee",
                    Quantity = 1,
                    UnitPrice = 12m,
                    TotalPrice = 12m,
                    LineNet = 10m,
                    VatAmount = 2m,
                    TaxRate = 20m,
                },
            ],
            TaxLines =
            [
                new ReceiptTaxLine
                {
                    TenantId = tenantId,
                    ReceiptId = receiptId,
                    TaxType = 1,
                    TaxRate = 20m,
                    NetAmount = 10m,
                    TaxAmount = 2m,
                    GrossAmount = 12m,
                },
            ],
        });
        await db.SaveChangesAsync();

        var service = new ReceiptService(
            db,
            NullLogger<ReceiptService>.Instance,
            Mock.Of<ITseService>(),
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions
            {
                CompanyName = "Live GmbH",
                TaxNumber = "ATU99999999",
                Street = "Live Str",
                ZipCode = "1020",
                City = "Wien",
            }),
            Mock.Of<IUserService>(),
            TenantTestDoubles.SettingsResolverReturning(tenantId),
            TenantTestDoubles.ProductionHostEnvironment);

        var dto = await service.GetReceiptAsync(receiptId);

        Assert.NotNull(dto);
        var item = Assert.Single(dto!.Items);
        Assert.Equal("Kaffee", item.Name);
        var tax = Assert.Single(dto.TaxRates);
        Assert.Equal(20m, tax.Rate);
        Assert.Equal("R-1", dto.ReceiptNumber);
    }
}
