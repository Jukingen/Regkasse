using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>Tax group catalog rules used by AdminTaxGroupsController (seed + system delete guard).</summary>
public sealed class AdminTaxGroupsCatalogRulesTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tax_groups_rules_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    [Fact]
    public async Task Seed_ThenCustomGroup_CanBeRemoved_SystemCannot()
    {
        await using var db = CreateDb();
        var tenantId = LegacyDefaultTenantIds.Primary;
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });
        await db.SaveChangesAsync();

        await TaxGroupSeedData.SeedSystemTaxGroupsAsync(db, tenantId);

        var custom = new TaxGroup
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Custom 5%",
            Rate = 5m,
            IsActive = true,
            IsSystem = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.TaxGroups.Add(custom);
        await db.SaveChangesAsync();

        var system = await db.TaxGroups.IgnoreQueryFilters()
            .FirstAsync(g => g.TenantId == tenantId && g.IsSystem);
        Assert.True(system.IsSystem);

        db.TaxGroups.Remove(custom);
        await db.SaveChangesAsync();
        Assert.False(await db.TaxGroups.IgnoreQueryFilters().AnyAsync(g => g.Id == custom.Id));

        // Controller refuses IsSystem deletes; entity remains seeded.
        Assert.Equal(5, await db.TaxGroups.IgnoreQueryFilters().CountAsync(g => g.TenantId == tenantId && g.IsSystem));
    }

    [Fact]
    public async Task OnlyOneDefault_AfterClearingOthers()
    {
        await using var db = CreateDb();
        var tenantId = LegacyDefaultTenantIds.Primary;
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });
        await db.SaveChangesAsync();
        await TaxGroupSeedData.SeedSystemTaxGroupsAsync(db, tenantId);

        var groups = await db.TaxGroups.IgnoreQueryFilters().Where(g => g.TenantId == tenantId).ToListAsync();
        Assert.Single(groups, g => g.IsDefault);

        var previous = groups.Single(g => g.IsDefault);
        var next = groups.First(g => !g.IsDefault);
        previous.IsDefault = false;
        next.IsDefault = true;
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.TaxGroups.IgnoreQueryFilters().CountAsync(g => g.TenantId == tenantId && g.IsDefault));
        Assert.Equal(next.Id, (await db.TaxGroups.IgnoreQueryFilters().SingleAsync(g => g.IsDefault)).Id);
    }
}
