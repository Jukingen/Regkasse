using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PlatformTenantRenameTests
{
    [Fact]
    public void SystemTenantIds_Reuse_Wave0_Guid_And_Platform_Slug()
    {
        Assert.Equal(Guid.Parse("9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c"), SystemTenantIds.Platform);
        Assert.Equal("platform", SystemTenantIds.PlatformSlug);
        Assert.True(SystemTenantIds.IsPlatformTenantId(SystemTenantIds.Platform));
        Assert.True(SystemTenantIds.IsPlatformSlug("platform"));
        Assert.False(SystemTenantIds.IsPlatformSlug("default"));
        Assert.False(SystemTenantIds.IsPlatformSlug("dev"));
    }

    [Theory]
    [InlineData("cafe")]
    [InlineData("bar")]
    [InlineData("test-cafe")]
    [InlineData("TEST_BAR")]
    public void LeftoverDemoTenantSlugs_Match_Legacy_Demo_Rows(string slug)
    {
        Assert.True(LeftoverDemoTenantSlugs.Matches(slug));
        Assert.False(LeftoverDemoTenantSlugs.Matches("dev"));
        Assert.False(LeftoverDemoTenantSlugs.Matches("prod"));
        Assert.False(LeftoverDemoTenantSlugs.Matches("platform"));
    }

    [Fact]
    public void DevTenantSlugAliases_No_Longer_Maps_Default_Or_Platform()
    {
        Assert.Equal("default", DevTenantSlugAliases.ResolveCanonical("default"));
        Assert.Equal("platform", DevTenantSlugAliases.ResolveCanonical("platform"));
        Assert.Equal("dev", DevTenantSlugAliases.ResolveCanonical("cafe"));
    }

    [Fact]
    public void ExcludeUnusedDefaultTenant_Hides_Platform_Slug()
    {
        var exclude = typeof(AdminTenantService).GetMethod(
            "ExcludeUnusedDefaultTenant",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(exclude);

        var now = DateTime.UtcNow;
        var items = new List<AdminTenantListItemDto>
        {
            new(
                SystemTenantIds.Platform,
                "Platform",
                SystemTenantIds.PlatformSlug,
                null,
                null,
                TenantStatuses.Active,
                false,
                null,
                null,
                now,
                null),
            new(
                DemoTenantIds.Dev,
                "Development",
                "dev",
                null,
                null,
                TenantStatuses.Active,
                true,
                null,
                null,
                now,
                null),
        };

        var filtered = (IReadOnlyList<AdminTenantListItemDto>)exclude!.Invoke(null, [items])!;

        Assert.DoesNotContain(filtered, t => SystemTenantIds.IsPlatformSlug(t.Slug));
        Assert.Contains(filtered, t => t.Slug == "dev");
    }

    [Fact]
    public void ExcludeUnusedDefaultTenant_Hides_Leftover_Cafe_And_Bar()
    {
        var exclude = typeof(AdminTenantService).GetMethod(
            "ExcludeUnusedDefaultTenant",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(exclude);

        var now = DateTime.UtcNow;
        var items = new List<AdminTenantListItemDto>
        {
            new(
                Guid.Parse("b0000001-0001-4001-8001-000000000099"),
                "Test Cafe",
                "cafe",
                null,
                null,
                TenantStatuses.Active,
                true,
                null,
                null,
                now,
                null),
            new(
                Guid.Parse("b0000001-0001-4001-8001-000000000098"),
                "Test Bar",
                "test-bar",
                null,
                null,
                TenantStatuses.Active,
                true,
                null,
                null,
                now,
                null),
            new(
                DemoTenantIds.Prod,
                "Production",
                "prod",
                null,
                null,
                TenantStatuses.Active,
                true,
                null,
                null,
                now,
                null),
        };

        var filtered = (IReadOnlyList<AdminTenantListItemDto>)exclude!.Invoke(null, [items])!;

        Assert.DoesNotContain(filtered, t => LeftoverDemoTenantSlugs.Matches(t.Slug));
        Assert.Contains(filtered, t => t.Slug == "prod");
    }

    [Fact]
    public async Task SoftDeleteAsync_Rejects_Platform_Tenant()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"platform_soft_delete_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
        db.Tenants.Add(new Tenant
        {
            Id = SystemTenantIds.Platform,
            Name = "Platform",
            Slug = SystemTenantIds.PlatformSlug,
            Status = TenantStatuses.Active,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = new TenantService(
            db,
            Mock.Of<IAuditLogService>(),
            Mock.Of<ITenantDeletionService>(),
            NullLogger<TenantService>.Instance);

        var (success, error) = await service.SoftDeleteAsync(SystemTenantIds.Platform, "actor-1");
        Assert.False(success);
        Assert.Contains("platform", error, StringComparison.OrdinalIgnoreCase);
    }
}
