using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Wave 3B: <see cref="ProductModifierGroup"/>, <see cref="ProductModifierGroupAssignment"/>, <see cref="AddOnGroupProduct"/> tenant alignment.
/// </summary>
public sealed class Wave3BTenantScopedModifierRelationsTests
{
    private static (AppDbContext Ctx, TenantTestDoubles.MutableTenantAccessor Accessor) CreateContextWithAccessor(Guid? ambientTenantId = null)
    {
        var accessor = new TenantTestDoubles.MutableTenantAccessor(ambientTenantId ?? TenantA);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Wave3B_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return (new AppDbContext(options, accessor), accessor);
    }

    private static AppDbContext CreateContext() => CreateContextWithAccessor().Ctx;

    private static readonly Guid TenantA = LegacyDefaultTenantIds.Primary;
    private static readonly Guid TenantB = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static void EnsureTwoTenants(AppDbContext ctx)
    {
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        if (!ctx.Tenants.AsNoTracking().Any(t => t.Id == TenantB))
        {
            ctx.Tenants.Add(new Tenant { Id = TenantB, Name = "Tenant B", Slug = "wave3b-test-tenant-b" });
        }
    }

    [Fact]
    public async Task SameModifierSetup_AllowedPerTenant_IsolatedRows()
    {
        await using var ctx = CreateContext();
        EnsureTwoTenants(ctx);
        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = "CA", VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = "CB", VatRate = 10m });
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();
        var mainA = Guid.NewGuid();
        var mainB = Guid.NewGuid();
        var addOnA = Guid.NewGuid();
        var addOnB = Guid.NewGuid();

        ctx.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupA, TenantId = TenantA, Name = "Extras", SortOrder = 0, IsActive = true });
        ctx.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupB, TenantId = TenantB, Name = "Extras", SortOrder = 0, IsActive = true });

        void AddProduct(Guid id, Guid catId, string catName, Guid tenant, string barcode)
        {
            ctx.Products.Add(new Product
            {
                Id = id,
                TenantId = tenant,
                Name = "P",
                Price = 1m,
                CategoryId = catId,
                Category = catName,
                StockQuantity = 1,
                MinStockLevel = 0,
                Unit = "Stk",
                TaxType = TaxTypes.Reduced,
                TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
                Barcode = barcode,
                IsFiscalCompliant = true,
                IsTaxable = true,
                RksvProductType = RksvProductTypes.Standard,
                IsActive = true,
                IsSellableAddOn = id == addOnA || id == addOnB
            });
        }

        AddProduct(mainA, catA, "CA", TenantA, "m-a");
        AddProduct(addOnA, catA, "CA", TenantA, "ao-a");
        AddProduct(mainB, catB, "CB", TenantB, "m-b");
        AddProduct(addOnB, catB, "CB", TenantB, "ao-b");

        ctx.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupA, ProductId = addOnA, TenantId = TenantA, SortOrder = 0 });
        ctx.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupB, ProductId = addOnB, TenantId = TenantB, SortOrder = 0 });
        ctx.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment { ProductId = mainA, ModifierGroupId = groupA, TenantId = TenantA, SortOrder = 0 });
        ctx.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment { ProductId = mainB, ModifierGroupId = groupB, TenantId = TenantB, SortOrder = 0 });
        await ctx.SaveChangesAsync();

        var forA = await ctx.ProductModifierGroupAssignments.IgnoreQueryFilters().CountAsync(a => a.TenantId == TenantA);
        var forB = await ctx.ProductModifierGroupAssignments.IgnoreQueryFilters().CountAsync(a => a.TenantId == TenantB);
        Assert.Equal(1, forA);
        Assert.Equal(1, forB);
    }

    [Fact]
    public async Task GetCatalog_ReturnsOnlyEffectiveTenantModifierGroups()
    {
        var (ctx, accessor) = CreateContextWithAccessor(TenantA);
        await using (ctx)
        {
            EnsureTwoTenants(ctx);
            var catA = Guid.NewGuid();
            var catB = Guid.NewGuid();
            ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = "CA", VatRate = 10m });
            ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = "CB", VatRate = 10m });

            var productOnlyB = Guid.NewGuid();
            var addOnB = Guid.NewGuid();
            var groupA = Guid.NewGuid();
            var groupB = Guid.NewGuid();

            ctx.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupA, TenantId = TenantA, Name = "TenantA Group", SortOrder = 0, IsActive = true });
            ctx.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupB, TenantId = TenantB, Name = "TenantB Group", SortOrder = 0, IsActive = true });

            ctx.Products.Add(new Product
            {
                Id = productOnlyB,
                TenantId = TenantB,
                Name = "MainB",
                Price = 5m,
                CategoryId = catB,
                Category = "CB",
                StockQuantity = 1,
                MinStockLevel = 0,
                Unit = "Stk",
                TaxType = TaxTypes.Reduced,
                TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
                Barcode = "cat-main-b",
                IsFiscalCompliant = true,
                IsTaxable = true,
                RksvProductType = RksvProductTypes.Standard,
                IsActive = true
            });
            ctx.Products.Add(new Product
            {
                Id = addOnB,
                TenantId = TenantB,
                Name = "AddB",
                Price = 0.5m,
                CategoryId = catB,
                Category = "CB",
                StockQuantity = 0,
                MinStockLevel = 0,
                Unit = "Stk",
                TaxType = TaxTypes.Reduced,
                TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
                Barcode = "cat-ao-b",
                IsFiscalCompliant = true,
                IsTaxable = true,
                RksvProductType = RksvProductTypes.Standard,
                IsActive = true,
                IsSellableAddOn = true
            });

            ctx.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment { ProductId = productOnlyB, ModifierGroupId = groupB, TenantId = TenantB, SortOrder = 0 });
            ctx.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupB, ProductId = addOnB, TenantId = TenantB, SortOrder = 0 });

            // Same assignment shape for tenant A on a product that is not in B's catalog
            var productOnlyA = Guid.NewGuid();
            var addOnA = Guid.NewGuid();
            ctx.Products.Add(new Product
            {
                Id = productOnlyA,
                TenantId = TenantA,
                Name = "MainA",
                Price = 3m,
                CategoryId = catA,
                Category = "CA",
                StockQuantity = 1,
                MinStockLevel = 0,
                Unit = "Stk",
                TaxType = TaxTypes.Reduced,
                TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
                Barcode = "cat-main-a",
                IsFiscalCompliant = true,
                IsTaxable = true,
                RksvProductType = RksvProductTypes.Standard,
                IsActive = true
            });
            ctx.Products.Add(new Product
            {
                Id = addOnA,
                TenantId = TenantA,
                Name = "AddA",
                Price = 0.25m,
                CategoryId = catA,
                Category = "CA",
                StockQuantity = 0,
                MinStockLevel = 0,
                Unit = "Stk",
                TaxType = TaxTypes.Reduced,
                TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
                Barcode = "cat-ao-a",
                IsFiscalCompliant = true,
                IsTaxable = true,
                RksvProductType = RksvProductTypes.Standard,
                IsActive = true,
                IsSellableAddOn = true
            });
            ctx.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment { ProductId = productOnlyA, ModifierGroupId = groupA, TenantId = TenantA, SortOrder = 0 });
            ctx.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupA, ProductId = addOnA, TenantId = TenantA, SortOrder = 0 });

            await ctx.SaveChangesAsync();

            accessor.TenantId = TenantB;

            var repo = new GenericRepository<Product>(ctx, NullLogger<GenericRepository<Product>>.Instance);
            var controller = new ProductController(ctx, repo, NullLogger<ProductController>.Instance, TenantTestDoubles.SettingsResolverReturning(TenantB));
            var identity = new System.Security.Claims.ClaimsIdentity(new[]
            {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "u1"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Cashier")
        }, "test");
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) }
            };

            var result = await controller.GetCatalog();
            var ok = Assert.IsType<OkObjectResult>(result);
            var dataProp = ok.Value!.GetType().GetProperty("data");
            Assert.NotNull(dataProp);
            var catalog = Assert.IsType<CatalogResponseDto>(dataProp.GetValue(ok.Value));
            Assert.NotNull(catalog.Products);
            var bProduct = catalog.Products.Single(p => p.Id == productOnlyB);
            Assert.NotNull(bProduct.ModifierGroups);
            Assert.Single(bProduct.ModifierGroups);
            Assert.Equal("TenantB Group", bProduct.ModifierGroups[0].Name);
        }
    }

    [Fact]
    public async Task LegacyPrimaryTenant_Data_StillLoadsAfterModelChange()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        var catId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var mainId = Guid.NewGuid();
        var addOnId = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catId, Name = "C", VatRate = 10m });
        ctx.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupId, TenantId = TenantA, Name = "G", SortOrder = 0, IsActive = true });
        ctx.Products.Add(new Product
        {
            Id = mainId,
            TenantId = TenantA,
            Name = "Main",
            Price = 2m,
            CategoryId = catId,
            Category = "C",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = "legacy-main",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        ctx.Products.Add(new Product
        {
            Id = addOnId,
            TenantId = TenantA,
            Name = "Add",
            Price = 0.1m,
            CategoryId = catId,
            Category = "C",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = "legacy-add",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true,
            IsSellableAddOn = true
        });
        ctx.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment { ProductId = mainId, ModifierGroupId = groupId, TenantId = TenantA, SortOrder = 0 });
        ctx.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupId, ProductId = addOnId, TenantId = TenantA, SortOrder = 0 });
        await ctx.SaveChangesAsync();

        var loaded = await ctx.ProductModifierGroups.Include(g => g.AddOnGroupProducts).FirstAsync(g => g.Id == groupId);
        Assert.Single(loaded.AddOnGroupProducts);
        Assert.Equal(TenantA, loaded.TenantId);
    }
}
