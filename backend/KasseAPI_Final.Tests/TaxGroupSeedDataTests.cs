using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TaxGroupSeedDataTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tax_group_seed_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    [Fact]
    public void GetTaxGroups_ReturnsFiveAustrianSystemRates()
    {
        var tenantId = Guid.NewGuid();
        var groups = TaxGroupSeedData.GetTaxGroups(tenantId);

        Assert.Equal(5, groups.Length);
        Assert.All(groups, g =>
        {
            Assert.Equal(tenantId, g.TenantId);
            Assert.True(g.IsSystem);
            Assert.True(g.IsActive);
        });
        Assert.Contains(groups, g => g.AustrianCode == "A" && g.Rate == 20m && g.IsDefault && g.GroupType == TaxGroupType.Standard);
        Assert.Contains(groups, g => g.AustrianCode == "B" && g.Rate == 10m && g.GroupType == TaxGroupType.Reduced);
        Assert.Contains(groups, g => g.AustrianCode == "C" && g.Rate == 4.9m && g.GroupType == TaxGroupType.ReducedNew);
        Assert.Contains(groups, g => g.AustrianCode == "D" && g.Rate == 13m && g.GroupType == TaxGroupType.Middle);
        Assert.Contains(groups, g => g.AustrianCode == "E" && g.Rate == 0m && g.GroupType == TaxGroupType.Zero);
        Assert.Single(groups, g => g.IsDefault);
    }

    [Fact]
    public async Task SeedSystemTaxGroupsAsync_CreatesFiveGroupsOnEmptyDatabase()
    {
        await using var db = CreateDb();
        var tenantId = SystemTenantIds.Platform;
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });
        await db.SaveChangesAsync();

        var created = await TaxGroupSeedData.SeedSystemTaxGroupsAsync(db, tenantId);

        Assert.Equal(5, created);
        Assert.Equal(5, await db.TaxGroups.IgnoreQueryFilters().CountAsync(g => g.TenantId == tenantId));
        Assert.Contains(
            await db.TaxGroups.IgnoreQueryFilters().ToListAsync(),
            g => g.Name == "Ermäßigt (Neu)" && g.Rate == 4.9m);
    }

    [Fact]
    public async Task SeedSystemTaxGroupsAsync_IsIdempotentWhenGroupsAlreadyExist()
    {
        await using var db = CreateDb();
        var tenantId = SystemTenantIds.Platform;
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });
        await db.SaveChangesAsync();

        var first = await TaxGroupSeedData.SeedSystemTaxGroupsAsync(db, tenantId);
        var second = await TaxGroupSeedData.SeedSystemTaxGroupsAsync(db, tenantId);

        Assert.Equal(5, first);
        Assert.Equal(0, second);
        Assert.Equal(5, await db.TaxGroups.IgnoreQueryFilters().CountAsync(g => g.TenantId == tenantId));
    }
}
