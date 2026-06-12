using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Dev;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DevTenantCatalogCleanupTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DevTenantCatalogCleanup_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new CurrentTenantAccessor { TenantId = LegacyDefaultTenantIds.Primary });
    }

    [Fact]
    public async Task ExecuteAsync_RemovesProductsAndCategoriesForTenant()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsureDefaultTenant(db);
        var catId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = catId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Salate",
            VatRate = 10m,
        });
        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "chefsalat",
            Price = 9.5m,
            CategoryId = catId,
            Category = "Salate",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = 10m,
            Barcode = "bc-1",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
        });
        await db.SaveChangesAsync();

        var result = await DevTenantCatalogCleanup.ExecuteAsync(
            db,
            LegacyDefaultTenantIds.Primary,
            includeCategories: true,
            allowFiscalOverride: false);

        Assert.Equal(1, result.ProductsDeleted);
        Assert.Equal(1, result.CategoriesDeleted);
        Assert.Empty(await db.Products.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.Categories.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_UnsignedPayments_DoNotBlockPurge()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsureDefaultTenant(db);
        var registerId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = LegacyDefaultTenantIds.Primary,
            RegisterNumber = "KASSE-001",
            Location = "Dev",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test",
            TableNumber = 1,
            CashierId = "cashier",
            TotalAmount = 5m,
            TaxAmount = 1m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            TseSignature = "",
            ReceiptNumber = "DEV-001",
            CreatedAt = DateTime.UtcNow,
        });
        var catId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = catId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "C",
            VatRate = 10m,
        });
        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Item",
            Price = 1m,
            CategoryId = catId,
            Category = "C",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = 10m,
            Barcode = "bc-unsigned",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
        });
        await db.SaveChangesAsync();

        var result = await DevTenantCatalogCleanup.ExecuteAsync(
            db,
            LegacyDefaultTenantIds.Primary,
            includeCategories: true,
            allowFiscalOverride: false);

        Assert.Equal(1, result.ProductsDeleted);
        Assert.False(result.HasFiscalPayments);
    }

    [Fact]
    public async Task ExecuteAsync_SignedPayments_BlockWithoutOverride()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsureDefaultTenant(db);
        var registerId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = LegacyDefaultTenantIds.Primary,
            RegisterNumber = "KASSE-001",
            Location = "Dev",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test",
            TableNumber = 1,
            CashierId = "cashier",
            TotalAmount = 5m,
            TaxAmount = 1m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            TseSignature = "header.payload.sig",
            TseTimestamp = DateTime.UtcNow,
            ReceiptNumber = "DEV-FISCAL-001",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevTenantCatalogCleanup.ExecuteAsync(
                db,
                LegacyDefaultTenantIds.Primary,
                includeCategories: true,
                allowFiscalOverride: false));

        Assert.Contains("signed fiscal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
