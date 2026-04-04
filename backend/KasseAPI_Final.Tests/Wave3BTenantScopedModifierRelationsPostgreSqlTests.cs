using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// PostgreSQL: composite FK on add-on links (not fully enforced by EF in-memory).
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

    private static async Task EnsureSecondaryTenantAsync(AppDbContext ctx)
    {
        if (!await ctx.Tenants.AnyAsync(t => t.Id == TenantB))
        {
            ctx.Tenants.Add(new Tenant { Id = TenantB, Name = "Wave3B B", Slug = "wave3b-pg-tenant-b" });
            await ctx.SaveChangesAsync();
        }
    }

    [SkippableFact]
    public async Task AddOnGroupProduct_CrossTenant_DatabaseRejectsCompositeForeignKey()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        await ctx.SaveChangesAsync();
        await EnsureSecondaryTenantAsync(ctx);

        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = catA, Name = "CA", VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = "CB", VatRate = 10m });
        var groupId = Guid.NewGuid();
        var productB = Guid.NewGuid();
        ctx.ProductModifierGroups.Add(new ProductModifierGroup
        {
            Id = groupId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "G",
            SortOrder = 0,
            IsActive = true
        });
        ctx.Products.Add(new Product
        {
            Id = productB,
            TenantId = TenantB,
            Name = "PB",
            Price = 1m,
            CategoryId = catB,
            Category = "CB",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = $"pg-wave3b-{productB:N}",
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
}
