using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Regression: Super Admin / settings-scoped reads must not be blocked by fail-closed ambient filters.
/// </summary>
public sealed class TenantFilterBypassContractTests
{
    [Fact]
    public async Task IgnoreQueryFilters_Sees_Sibling_Tenant_Rows_When_Ambient_Is_Other_Tenant()
    {
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var accessor = new TenantTestDoubles.MutableTenantAccessor(tenantA);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FilterBypass_{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options, accessor);
        db.Tenants.AddRange(
            new Tenant { Id = tenantA, Name = "A", Slug = "a", Status = TenantStatuses.Active, IsActive = true },
            new Tenant { Id = tenantB, Name = "B", Slug = "b", Status = TenantStatuses.Active, IsActive = true });
        db.Categories.AddRange(
            new Category { Id = Guid.NewGuid(), TenantId = tenantA, Name = "CatA", VatRate = 10m },
            new Category { Id = Guid.NewGuid(), TenantId = tenantB, Name = "CatB", VatRate = 10m });
        await db.SaveChangesAsync();

        Assert.Single(await db.Categories.ToListAsync());
        Assert.Equal(2, await db.Categories.IgnoreQueryFilters().CountAsync());
        Assert.Equal(
            "CatB",
            (await db.Categories.IgnoreQueryFilters().SingleAsync(c => c.TenantId == tenantB)).Name);
    }

    [Fact]
    public void ManagerOversightViewPermissions_AreCoveredByMatrixOrImplication()
    {
        var managerPerms = RolePermissionMatrix.GetPermissionsForRole(Roles.Manager);
        foreach (var key in AdminAppPermissionProfile.ManagerOversightViewPermissions)
        {
            Assert.True(
                PermissionImplication.IsSatisfied(key, managerPerms),
                $"Manager matrix/implications missing oversight key '{key}'.");
        }
    }
}
