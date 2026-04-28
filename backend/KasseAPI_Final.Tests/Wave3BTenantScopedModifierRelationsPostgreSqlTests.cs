using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// PostgreSQL: composite tenant FK on <see cref="ProductModifierGroupAssignment"/> and <see cref="AddOnGroupProduct"/> (not fully enforced by EF in-memory).
/// </summary>
[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class Wave3BTenantScopedModifierRelationsPostgreSqlTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public Wave3BTenantScopedModifierRelationsPostgreSqlTests(PostgreSqlReplayFixture fixture) =>
        _fixture = fixture;

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(_fixture.ConnectionString).Options);

    private static readonly Guid TenantB = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static readonly Guid TenantA = LegacyDefaultTenantIds.Primary;

    private static async Task EnsureSecondaryTenantAsync(AppDbContext ctx)
    {
        if (!await ctx.Tenants.AnyAsync(t => t.Id == TenantB))
        {
            ctx.Tenants.Add(new Tenant { Id = TenantB, Name = "Wave3B B", Slug = "wave3b-pg-tenant-b" });
            await ctx.SaveChangesAsync();
        }
    }

    /// <summary>Group in Tenant A (primary), product in Tenant B, link row stamped Tenant B — violates group composite FK.</summary>
    [SkippableFact]
    public async Task AddOnGroupProduct_TenantAModifierGroup_TenantBProduct_DatabaseRejectsCompositeForeignKey()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        await ctx.SaveChangesAsync();
        await EnsureSecondaryTenantAsync(ctx);

        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = "CA", VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = "CB", VatRate = 10m });
        var groupId = Guid.NewGuid();
        var productB = Guid.NewGuid();
        ctx.ProductModifierGroups.Add(new ProductModifierGroup
        {
            Id = groupId,
            TenantId = TenantA,
            Name = "G",
            SortOrder = 0,
            IsActive = true
        });
        ctx.Products.Add(new Product
        {
            Id = productB,
            TenantId = TenantB,
            Name = "PB",
            Description = "-",
            Price = 1m,
            CategoryId = catB,
            Category = "CB",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = $"M1{productB:N[..11]}",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true,
            IsSellableAddOn = true
        });
        await ctx.SaveChangesAsync();

        ctx.AddOnGroupProducts.Add(new AddOnGroupProduct
        {
            ModifierGroupId = groupId,
            ProductId = productB,
            TenantId = TenantB,
            SortOrder = 0
        });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    /// <summary>Product in Tenant A, modifier group in Tenant B; assignment stamped Tenant A — violates group composite FK.</summary>
    [SkippableFact]
    public async Task ProductModifierGroupAssignment_TenantAProduct_TenantBModifierGroup_DatabaseRejectsCompositeForeignKey()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        await ctx.SaveChangesAsync();
        await EnsureSecondaryTenantAsync(ctx);

        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = "CA", VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = "CB", VatRate = 10m });
        var groupB = Guid.NewGuid();
        var productA = Guid.NewGuid();
        ctx.ProductModifierGroups.Add(new ProductModifierGroup
        {
            Id = groupB,
            TenantId = TenantB,
            Name = "GB",
            SortOrder = 0,
            IsActive = true
        });
        ctx.Products.Add(new Product
        {
            Id = productA,
            TenantId = TenantA,
            Name = "PA",
            Description = "-",
            Price = 1m,
            CategoryId = catA,
            Category = "CA",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = $"M2{productA:N[..11]}",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        ctx.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment
        {
            ProductId = productA,
            ModifierGroupId = groupB,
            TenantId = TenantA,
            SortOrder = 0
        });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    /// <summary>Product in Tenant A, modifier group in Tenant B; add-on link stamped Tenant A — violates group composite FK.</summary>
    [SkippableFact]
    public async Task AddOnGroupProduct_TenantAProduct_TenantBModifierGroup_DatabaseRejectsCompositeForeignKey()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        await ctx.SaveChangesAsync();
        await EnsureSecondaryTenantAsync(ctx);

        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = "CA", VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = "CB", VatRate = 10m });
        var groupB = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var addOnA = Guid.NewGuid();
        ctx.ProductModifierGroups.Add(new ProductModifierGroup
        {
            Id = groupB,
            TenantId = TenantB,
            Name = "GB",
            SortOrder = 0,
            IsActive = true
        });
        ctx.Products.Add(new Product
        {
            Id = productA,
            TenantId = TenantA,
            Name = "PA",
            Description = "-",
            Price = 2m,
            CategoryId = catA,
            Category = "CA",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = $"M3{productA:N[..11]}",
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
            Description = "-",
            Price = 0.5m,
            CategoryId = catA,
            Category = "CA",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = $"M4{addOnA:N[..11]}",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true,
            IsSellableAddOn = true
        });
        await ctx.SaveChangesAsync();

        ctx.AddOnGroupProducts.Add(new AddOnGroupProduct
        {
            ModifierGroupId = groupB,
            ProductId = addOnA,
            TenantId = TenantA,
            SortOrder = 0
        });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task ProductModifierGroupAssignment_AndAddOnGroupProduct_TwoTenantsSeparateGuids_DatabaseAllowsBoth()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        await ctx.SaveChangesAsync();
        await EnsureSecondaryTenantAsync(ctx);

        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = "CA2", VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = "CB2", VatRate = 10m });

        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();
        var mainA = Guid.NewGuid();
        var mainB = Guid.NewGuid();
        var addOnA = Guid.NewGuid();
        var addOnB = Guid.NewGuid();

        ctx.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupA, TenantId = TenantA, Name = "GA", SortOrder = 0, IsActive = true });
        ctx.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupB, TenantId = TenantB, Name = "GB", SortOrder = 0, IsActive = true });

        void AddProduct(Guid id, Guid catId, string catName, Guid tenant, string barcode, bool addOn)
        {
            ctx.Products.Add(new Product
            {
                Id = id,
                TenantId = tenant,
                Name = "P",
                Description = "-",
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
                IsSellableAddOn = addOn
            });
        }

        AddProduct(mainA, catA, "CA2", TenantA, $"pg-w3b-2t-mainA-{mainA:N}", false);
        AddProduct(addOnA, catA, "CA2", TenantA, $"pg-w3b-2t-aoA-{addOnA:N}", true);
        AddProduct(mainB, catB, "CB2", TenantB, $"pg-w3b-2t-mainB-{mainB:N}", false);
        AddProduct(addOnB, catB, "CB2", TenantB, $"pg-w3b-2t-aoB-{addOnB:N}", true);

        ctx.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment { ProductId = mainA, ModifierGroupId = groupA, TenantId = TenantA, SortOrder = 0 });
        ctx.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment { ProductId = mainB, ModifierGroupId = groupB, TenantId = TenantB, SortOrder = 0 });
        ctx.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupA, ProductId = addOnA, TenantId = TenantA, SortOrder = 0 });
        ctx.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupB, ProductId = addOnB, TenantId = TenantB, SortOrder = 0 });

        await ctx.SaveChangesAsync();

        Assert.Equal(1, await ctx.ProductModifierGroupAssignments.CountAsync(a => a.TenantId == TenantA));
        Assert.Equal(1, await ctx.ProductModifierGroupAssignments.CountAsync(a => a.TenantId == TenantB));
        Assert.Equal(2, await ctx.AddOnGroupProducts.CountAsync(a => a.ModifierGroupId == groupA || a.ModifierGroupId == groupB));
    }

    /// <summary>Rows stamped with legacy default tenant remain readable (post-migration single-tenant shape).</summary>
    [SkippableFact]
    public async Task LegacyDefaultTenant_ModifierGroupAssignment_AndAddOnGroupProductRows_RemainQueryable_AfterInsert()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        await ctx.SaveChangesAsync();

        var catId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var mainId = Guid.NewGuid();
        var addOnId = Guid.NewGuid();

        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catId, Name = "CLegacy", VatRate = 10m });
        ctx.ProductModifierGroups.Add(new ProductModifierGroup
        {
            Id = groupId,
            TenantId = TenantA,
            Name = "GLegacy",
            SortOrder = 0,
            IsActive = true
        });
        ctx.Products.Add(new Product
        {
            Id = mainId,
            TenantId = TenantA,
            Name = "Main",
            Description = "-",
            Price = 3m,
            CategoryId = catId,
            Category = "CLegacy",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = $"LM{mainId:N[..12]}",
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
            Description = "-",
            Price = 0.25m,
            CategoryId = catId,
            Category = "CLegacy",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = $"LA{addOnId:N[..12]}",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true,
            IsSellableAddOn = true
        });
        ctx.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment { ProductId = mainId, ModifierGroupId = groupId, TenantId = TenantA, SortOrder = 0 });
        ctx.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupId, ProductId = addOnId, TenantId = TenantA, SortOrder = 0 });
        await ctx.SaveChangesAsync();

        var group = await ctx.ProductModifierGroups
            .AsNoTracking()
            .Include(g => g.ProductAssignments)
            .Include(g => g.AddOnGroupProducts)
            .FirstAsync(g => g.Id == groupId && g.TenantId == TenantA);

        Assert.Single(group.ProductAssignments);
        Assert.Equal(mainId, group.ProductAssignments.First().ProductId);
        Assert.Single(group.AddOnGroupProducts);
        Assert.Equal(addOnId, group.AddOnGroupProducts.First().ProductId);
    }
}
