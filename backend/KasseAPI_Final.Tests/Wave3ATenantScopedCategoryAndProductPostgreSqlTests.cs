using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// PostgreSQL-backed checks for Wave 3A unique indexes and composite FK (not enforced by EF in-memory provider).
/// </summary>
[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class Wave3ATenantScopedCategoryAndProductPostgreSqlTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public Wave3ATenantScopedCategoryAndProductPostgreSqlTests(PostgreSqlReplayFixture fixture) =>
        _fixture = fixture;

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(_fixture.ConnectionString).Options);

    private static readonly Guid TenantB = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static async Task EnsureSecondaryTenantAsync(AppDbContext ctx)
    {
        if (!await ctx.Tenants.AnyAsync(t => t.Id == TenantB))
        {
            ctx.Tenants.Add(new Tenant { Id = TenantB, Name = "Wave3A B", Slug = "wave3a-pg-tenant-b" });
            await ctx.SaveChangesAsync();
        }
    }

    [SkippableFact]
    public async Task Category_SameName_SameTenant_DatabaseRejectsDuplicate()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        await ctx.SaveChangesAsync();

        ctx.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Name = "DupPg", VatRate = 10m });
        await ctx.SaveChangesAsync();
        ctx.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Name = "DupPg", VatRate = 10m });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task Product_SameBarcode_SameTenant_DatabaseRejectsDuplicate()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        await ctx.SaveChangesAsync();

        var catId = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = catId, Name = "CPg", VatRate = 10m });
        await ctx.SaveChangesAsync();
        const string barcode = "DUP-PG-BC";
        ctx.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "P1",
            Description = "-",
            Price = 1m,
            CategoryId = catId,
            Category = "CPg",
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
        ctx.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "P2",
            Description = "-",
            Price = 2m,
            CategoryId = catId,
            Category = "CPg",
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
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task Product_CategoryFromOtherTenant_DatabaseRejectsCompositeForeignKey()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        await ctx.SaveChangesAsync();
        await EnsureSecondaryTenantAsync(ctx);

        var catB = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = "OnlyB", VatRate = 10m });
        await ctx.SaveChangesAsync();

        ctx.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Cross",
            Description = "-",
            Price = 1m,
            CategoryId = catB,
            Category = "OnlyB",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = "cross-pg-1",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }
}
