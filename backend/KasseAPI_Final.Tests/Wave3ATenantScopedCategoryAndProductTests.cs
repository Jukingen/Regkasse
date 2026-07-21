using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Wave 3A: <see cref="Category"/> and <see cref="Product"/> tenant scoping, uniqueness, composite FK, and repository stamping.
/// </summary>
public sealed class Wave3ATenantScopedCategoryAndProductTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Wave3A_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static readonly Guid TenantA = LegacyDefaultTenantIds.Primary;
    private static readonly Guid TenantB = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static void EnsureTwoTenants(AppDbContext ctx)
    {
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        if (!ctx.Tenants.AsNoTracking().Any(t => t.Id == TenantB))
        {
            ctx.Tenants.Add(new Tenant { Id = TenantB, Name = "Tenant B", Slug = "wave3a-test-tenant-b" });
        }
    }

    [Fact]
    public async Task ProductRepository_AddAsync_StampsTenantFromResolver()
    {
        await using var ctx = CreateContext();
        EnsureTwoTenants(ctx);
        var catId = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catId, Name = "C", VatRate = 10m });
        await ctx.SaveChangesAsync();

        var repo = new TenantScopedProductRepository(
            ctx,
            Mock.Of<ILogger<GenericRepository<Product>>>(),
            TenantTestDoubles.SettingsResolverReturning(TenantB));

        var added = await repo.AddAsync(new Product
        {
            Id = Guid.NewGuid(),
            Name = "P",
            Price = 1m,
            CategoryId = catId,
            Category = "C",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = "bc-repo-add",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            TenantId = Guid.Empty
        });

        Assert.Equal(TenantB, added.TenantId);
    }

    [Fact]
    public async Task ProductRepository_GetAllAndGetById_OnlySeeOwnTenant()
    {
        await using var ctx = CreateContext();
        EnsureTwoTenants(ctx);
        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        var pA = Guid.NewGuid();
        var pB = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = "CA", VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = "CB", VatRate = 10m });
        ctx.Products.Add(new Product
        {
            Id = pA,
            TenantId = TenantA,
            Name = "PA",
            Price = 1m,
            CategoryId = catA,
            Category = "CA",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = "bc-a-1",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        ctx.Products.Add(new Product
        {
            Id = pB,
            TenantId = TenantB,
            Name = "PB",
            Price = 2m,
            CategoryId = catB,
            Category = "CB",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = "bc-b-1",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var repoA = new TenantScopedProductRepository(
            ctx,
            Mock.Of<ILogger<GenericRepository<Product>>>(),
            TenantTestDoubles.SettingsResolverReturning(TenantA));
        var listA = await repoA.GetAllAsync();
        Assert.Single(listA);
        Assert.Equal(pA, listA.First().Id);
        Assert.Null(await repoA.GetByIdAsync(pB));

        var repoB = new TenantScopedProductRepository(
            ctx,
            Mock.Of<ILogger<GenericRepository<Product>>>(),
            TenantTestDoubles.SettingsResolverReturning(TenantB));
        var listB = await repoB.GetAllAsync();
        Assert.Single(listB);
        Assert.Equal(pB, listB.First().Id);
    }

    [Fact]
    public async Task Category_SameName_TwoTenants_Allowed()
    {
        await using var ctx = CreateContext();
        EnsureTwoTenants(ctx);
        ctx.Categories.Add(new Category { TenantId = TenantA, Name = "Food", VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Name = "Food", VatRate = 10m });
        await ctx.SaveChangesAsync();
        Assert.Equal(2, await ctx.Categories.IgnoreQueryFilters().CountAsync(c => c.Name == "Food"));
    }

    [Fact]
    public async Task CreateCategory_DuplicateNameSameTenant_ReturnsConflict()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        ctx.Categories.Add(new Category { TenantId = TenantA, Name = "Existing", VatRate = 10m });
        await ctx.SaveChangesAsync();

        var controller = new CategoriesController(
            ctx,
            NullLogger<CategoriesController>.Instance,
            TenantTestDoubles.PrimaryTenantResolver,
            Mock.Of<IAuditLogService>(),
            Mock.Of<ICategoryDemoResetService>());
        var dup = await controller.CreateCategory(new CreateCategoryRequest { Name = "Existing", VatRate = 10m });
        var conflict = Assert.IsAssignableFrom<ObjectResult>(dup.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_DuplicateNameInactiveCategorySameTenant_ReturnsConflict()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        ctx.Categories.Add(new Category { TenantId = TenantA, Name = "Archived", VatRate = 10m, IsActive = false });
        await ctx.SaveChangesAsync();

        var controller = new CategoriesController(
            ctx,
            NullLogger<CategoriesController>.Instance,
            TenantTestDoubles.PrimaryTenantResolver,
            Mock.Of<IAuditLogService>(),
            Mock.Of<ICategoryDemoResetService>());
        var dup = await controller.CreateCategory(new CreateCategoryRequest { Name = "archived", VatRate = 10m });
        var conflict = Assert.IsAssignableFrom<ObjectResult>(dup.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Product_SameBarcode_TwoTenants_Allowed()
    {
        await using var ctx = CreateContext();
        EnsureTwoTenants(ctx);
        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = "A", VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = "B", VatRate = 10m });
        const string barcode = "SHARED-BC";
        ctx.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            Name = "P1",
            Price = 1m,
            CategoryId = catA,
            Category = "A",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = barcode,
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        ctx.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = TenantB,
            Name = "P2",
            Price = 1m,
            CategoryId = catB,
            Category = "B",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = barcode,
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task CategoriesController_CreateCategory_StampsEffectiveTenant()
    {
        await using var ctx = CreateContext();
        EnsureTwoTenants(ctx);
        var controller = new CategoriesController(
            ctx,
            NullLogger<CategoriesController>.Instance,
            TenantTestDoubles.SettingsResolverReturning(TenantB),
            Mock.Of<IAuditLogService>(),
            Mock.Of<ICategoryDemoResetService>());
        var res = await controller.CreateCategory(new CreateCategoryRequest { Name = "Fresh", VatRate = 10m });
        var created = Assert.IsType<CreatedAtActionResult>(res.Result);
        var cat = Assert.IsType<CategoryDto>(created.Value);
        var stored = await ctx.Categories.IgnoreQueryFilters().SingleAsync(c => c.Id == cat.Id);
        Assert.Equal(TenantB, stored.TenantId);
    }

    [Fact]
    public async Task CategoriesController_GetCategory_OtherTenant_ReturnsNotFound()
    {
        await using var ctx = CreateContext();
        EnsureTwoTenants(ctx);
        var catBId = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catBId, Name = "Hidden", VatRate = 10m });
        await ctx.SaveChangesAsync();

        var controller = new CategoriesController(
            ctx,
            NullLogger<CategoriesController>.Instance,
            TenantTestDoubles.SettingsResolverReturning(TenantA),
            Mock.Of<IAuditLogService>(),
            Mock.Of<ICategoryDemoResetService>());
        var res = await controller.GetCategory(catBId);
        Assert.IsType<NotFoundObjectResult>(res.Result);
    }

    [Fact]
    public async Task LegacyPrimaryTenant_Data_StillQueryableByResolver()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        var catId = Guid.NewGuid();
        var pId = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catId, Name = "Legacy", VatRate = 10m });
        ctx.Products.Add(new Product
        {
            Id = pId,
            TenantId = TenantA,
            Name = "Item",
            Price = 3m,
            CategoryId = catId,
            Category = "Legacy",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = "legacy-bc",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var repo = new TenantScopedProductRepository(
            ctx,
            Mock.Of<ILogger<GenericRepository<Product>>>(),
            TenantTestDoubles.PrimaryTenantResolver);
        var got = await repo.GetByIdAsync(pId);
        Assert.NotNull(got);
        Assert.Equal("Item", got!.Name);
    }
}
