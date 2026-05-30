using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminProductListFilterTests
{
    [Fact]
    public void BuildFilterSummary_CountsActiveFilters()
    {
        var filter = new ProductFilterDto
        {
            SearchTerm = "cola",
            MinPrice = 1m,
            MaxPrice = 10m,
            TaxTypes = [TaxTypes.Reduced],
            CategoryIds = [Guid.NewGuid()],
            StockStatus = StockFilterType.LowStock,
        };

        var summary = ProductQueryExtensions.BuildFilterSummary(filter);

        Assert.Equal(6, summary.ActiveFilterCount);
        Assert.Contains("searchTerm", summary.AppliedFilters.Keys);
        Assert.Equal(Models.TaxTypes.All.ToList(), summary.AvailableTaxTypes);
    }

    [Fact]
    public async Task ApplyPriceAndTaxFilters_ReturnsMatchingProducts()
    {
        await using var db = CreateContext();
        var catId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = catId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Drinks",
            VatRate = 10m,
        });

        var matchId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        db.Products.AddRange(
            NewProduct(matchId, "Match", 5m, TaxTypes.Reduced, catId),
            NewProduct(otherId, "Other", 25m, TaxTypes.Standard, catId));
        await db.SaveChangesAsync();

        var filter = new ProductFilterDto
        {
            MinPrice = 3m,
            MaxPrice = 10m,
            TaxTypes = [TaxTypes.Reduced],
        };

        var query = db.Products.AsNoTracking()
            .ApplyTenantScope(LegacyDefaultTenantIds.Primary)
            .ApplyPriceRangeFilter(filter)
            .ApplyTaxTypeFilter(filter.TaxTypes);

        var ids = await query.Select(p => p.Id).ToListAsync();
        Assert.Single(ids);
        Assert.Equal(matchId, ids[0]);
    }

    [Fact]
    public async Task ApplyStockStatusFilter_LowStock_UsesMinStockLevel()
    {
        await using var db = CreateContext();
        var catId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = catId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Food",
            VatRate = 10m,
        });

        var lowId = Guid.NewGuid();
        var okId = Guid.NewGuid();
        db.Products.AddRange(
            NewProduct(lowId, "Low", 2m, TaxTypes.Reduced, catId, stock: 1, minStock: 5),
            NewProduct(okId, "Ok", 2m, TaxTypes.Reduced, catId, stock: 10, minStock: 5));
        await db.SaveChangesAsync();

        var filter = new ProductFilterDto { StockStatus = StockFilterType.LowStock };
        var ids = await db.Products.AsNoTracking()
            .ApplyStockStatusFilter(filter)
            .Select(p => p.Id)
            .ToListAsync();

        Assert.Single(ids);
        Assert.Equal(lowId, ids[0]);
    }

    [Fact]
    public async Task QueryAsync_InvalidPriceRange_ReturnsError()
    {
        await using var db = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(db);
        var service = new AdminProductListService(db, TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary));

        var (_, code, _) = await service.QueryAsync(new ProductFilterDto { MinPrice = 20m, MaxPrice = 5m });

        Assert.Equal("ADMIN_PRODUCTS_INVALID_PRICE_RANGE", code);
    }

    [Fact]
    public async Task ApplyStockStatusFilter_Overstock_UsesProductMaxStockLevel()
    {
        await using var db = CreateContext();
        var catId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = catId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Food",
            VatRate = 10m,
        });

        var overId = Guid.NewGuid();
        var okId = Guid.NewGuid();
        db.Products.AddRange(
            NewProduct(overId, "Over", 2m, TaxTypes.Reduced, catId, stock: 20, minStock: 1, maxStock: 10),
            NewProduct(okId, "Ok", 2m, TaxTypes.Reduced, catId, stock: 8, minStock: 1, maxStock: 10));
        await db.SaveChangesAsync();

        var filter = new ProductFilterDto { StockStatus = StockFilterType.Overstock };
        var ids = await db.Products.AsNoTracking()
            .ApplyStockStatusFilter(filter)
            .Select(p => p.Id)
            .ToListAsync();

        Assert.Single(ids);
        Assert.Equal(overId, ids[0]);
    }

    private static Product NewProduct(
        Guid id,
        string name,
        decimal price,
        int taxType,
        Guid categoryId,
        int stock = 5,
        int minStock = 0,
        int? maxStock = null) => new()
    {
        Id = id,
        TenantId = LegacyDefaultTenantIds.Primary,
        Name = name,
        Price = price,
        CategoryId = categoryId,
        Category = "C",
        StockQuantity = stock,
        MinStockLevel = minStock,
        MaxStockLevel = maxStock,
        Unit = "Stk",
        TaxType = taxType,
        TaxRate = TaxTypes.GetTaxRate(taxType),
        Barcode = $"bc-{id:N}",
        IsFiscalCompliant = true,
        IsTaxable = true,
        RksvProductType = RksvProductTypes.Standard,
        IsActive = true,
    };

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new AppDbContext(options);
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        return ctx;
    }
}
