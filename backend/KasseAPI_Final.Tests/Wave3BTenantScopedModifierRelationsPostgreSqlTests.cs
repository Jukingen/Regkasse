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

    private static readonly Guid TenantA = SystemTenantIds.Platform;

    private static string Barcode(string prefix, Guid id) => $"{prefix}{id.ToString("N")[..11]}";

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
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        await ctx.SaveChangesAsync();
        await EnsureSecondaryTenantAsync(ctx);

        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        var taxB = TenantTestDoubles.EnsureTaxGroup(ctx, TenantB);
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = $"CA-{catA:N}"[..20], VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = $"CB-{catB:N}"[..20], VatRate = 10m });
        var groupId = Guid.NewGuid();
        var productB = Guid.NewGuid();
        ctx.ProductModifierGroups.Add(new ProductModifierGroup
        {
            Id = groupId,
            TenantId = TenantA,
            Name = $"G-{groupId:N}"[..20],
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
            TaxGroupId = taxB,
            Category = "CB",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = Barcode("M1", productB),
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
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        await ctx.SaveChangesAsync();
        await EnsureSecondaryTenantAsync(ctx);

        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        var taxA = TenantTestDoubles.EnsureTaxGroup(ctx, TenantA);
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = $"CA-{catA:N}"[..20], VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = $"CB-{catB:N}"[..20], VatRate = 10m });
        var groupB = Guid.NewGuid();
        var productA = Guid.NewGuid();
        ctx.ProductModifierGroups.Add(new ProductModifierGroup
        {
            Id = groupB,
            TenantId = TenantB,
            Name = $"GB-{groupB:N}"[..20],
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
            TaxGroupId = taxA,
            Category = "CA",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = Barcode("M2", productA),
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
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        await ctx.SaveChangesAsync();
        await EnsureSecondaryTenantAsync(ctx);

        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        var taxA = TenantTestDoubles.EnsureTaxGroup(ctx, TenantA);
        var taxB = TenantTestDoubles.EnsureTaxGroup(ctx, TenantB);
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = $"CA-{catA:N}"[..20], VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = $"CB-{catB:N}"[..20], VatRate = 10m });
        var groupB = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var addOnA = Guid.NewGuid();
        ctx.ProductModifierGroups.Add(new ProductModifierGroup
        {
            Id = groupB,
            TenantId = TenantB,
            Name = $"GB-{groupB:N}"[..20],
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
            TaxGroupId = taxA,
            Category = "CA",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = Barcode("M3", productA),
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
            TaxGroupId = taxA,
            Category = "CA",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = Barcode("M4", addOnA),
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
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        await ctx.SaveChangesAsync();
        await EnsureSecondaryTenantAsync(ctx);

        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        var taxA = TenantTestDoubles.EnsureTaxGroup(ctx, TenantA);
        var taxB = TenantTestDoubles.EnsureTaxGroup(ctx, TenantB);
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = $"CA2-{catA:N}"[..20], VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = $"CB2-{catB:N}"[..20], VatRate = 10m });

        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();
        var mainA = Guid.NewGuid();
        var mainB = Guid.NewGuid();
        var addOnA = Guid.NewGuid();
        var addOnB = Guid.NewGuid();

        ctx.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupA, TenantId = TenantA, Name = $"GA-{groupA:N}"[..20], SortOrder = 0, IsActive = true });
        ctx.ProductModifierGroups.Add(new ProductModifierGroup { Id = groupB, TenantId = TenantB, Name = $"GB-{groupB:N}"[..20], SortOrder = 0, IsActive = true });

        void AddProduct(Guid id, Guid catId, string catName, Guid tenant, Guid taxGroupId, string barcode, bool addOn)
        {
            ctx.Products.Add(new Product
            {
                Id = id,
                TenantId = tenant,
                Name = $"P-{id:N}"[..12],
                Description = "-",
                Price = 1m,
                CategoryId = catId,
                TaxGroupId = taxGroupId,
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

        AddProduct(mainA, catA, "CA2", TenantA, taxA, $"w3b-a-{mainA:N}"[..32], false);
        AddProduct(addOnA, catA, "CA2", TenantA, taxA, $"w3b-aoa-{addOnA:N}"[..32], true);
        AddProduct(mainB, catB, "CB2", TenantB, taxB, $"w3b-b-{mainB:N}"[..32], false);
        AddProduct(addOnB, catB, "CB2", TenantB, taxB, $"w3b-aob-{addOnB:N}"[..32], true);

        ctx.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment { ProductId = mainA, ModifierGroupId = groupA, TenantId = TenantA, SortOrder = 0 });
        ctx.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment { ProductId = mainB, ModifierGroupId = groupB, TenantId = TenantB, SortOrder = 0 });
        ctx.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupA, ProductId = addOnA, TenantId = TenantA, SortOrder = 0 });
        ctx.AddOnGroupProducts.Add(new AddOnGroupProduct { ModifierGroupId = groupB, ProductId = addOnB, TenantId = TenantB, SortOrder = 0 });

        await ctx.SaveChangesAsync();

        Assert.Equal(1, await ctx.ProductModifierGroupAssignments.IgnoreQueryFilters().CountAsync(a => a.ProductId == mainA && a.ModifierGroupId == groupA));
        Assert.Equal(1, await ctx.ProductModifierGroupAssignments.IgnoreQueryFilters().CountAsync(a => a.ProductId == mainB && a.ModifierGroupId == groupB));
        Assert.Equal(1, await ctx.AddOnGroupProducts.IgnoreQueryFilters().CountAsync(a => a.ModifierGroupId == groupA && a.ProductId == addOnA));
        Assert.Equal(1, await ctx.AddOnGroupProducts.IgnoreQueryFilters().CountAsync(a => a.ModifierGroupId == groupB && a.ProductId == addOnB));
    }

    /// <summary>Rows stamped with legacy default tenant remain readable (post-migration single-tenant shape).</summary>
    [SkippableFact]
    public async Task LegacyDefaultTenant_ModifierGroupAssignment_AndAddOnGroupProductRows_RemainQueryable_AfterInsert()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        await ctx.SaveChangesAsync();

        var catId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var mainId = Guid.NewGuid();
        var addOnId = Guid.NewGuid();
        var taxA = TenantTestDoubles.EnsureTaxGroup(ctx, TenantA);

        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catId, Name = $"CLegacy-{catId:N}"[..20], VatRate = 10m });
        ctx.ProductModifierGroups.Add(new ProductModifierGroup
        {
            Id = groupId,
            TenantId = TenantA,
            Name = $"GLegacy-{groupId:N}"[..20],
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
            TaxGroupId = taxA,
            Category = "CLegacy",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = Barcode("LM", mainId),
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
            TaxGroupId = taxA,
            Category = "CLegacy",
            StockQuantity = 0,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = Barcode("LA", addOnId),
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
            .IgnoreQueryFilters()
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
