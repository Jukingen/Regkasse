using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class UserTenantMembershipHealSeedTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"utm_heal_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    [Fact]
    public async Task Heal_Deactivates_Legacy_Default_When_User_Also_Has_Active_Dev()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = LegacyDefaultTenantIds.Primary,
            Name = "Default",
            Slug = LegacyDefaultTenantIds.PrimarySlug,
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "manager1",
            TenantId = LegacyDefaultTenantIds.Primary,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "manager1",
            TenantId = DemoTenantIds.Dev,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var healed = await UserTenantMembershipHealSeed.HealLegacyDefaultAlongsideDemoTenantsCoreAsync(db);

        Assert.Equal(1, healed);
        var rows = await db.UserTenantMemberships.IgnoreQueryFilters()
            .Where(m => m.UserId == "manager1")
            .OrderBy(m => m.TenantId)
            .ToListAsync();
        Assert.False(rows.Single(m => m.TenantId == LegacyDefaultTenantIds.Primary).IsActive);
        Assert.True(rows.Single(m => m.TenantId == DemoTenantIds.Dev).IsActive);
    }

    [Fact]
    public async Task Heal_Leaves_Legacy_Default_Alone_When_No_Demo_Membership()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = LegacyDefaultTenantIds.Primary,
            Name = "Default",
            Slug = LegacyDefaultTenantIds.PrimarySlug,
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "demo@demo.com",
            TenantId = LegacyDefaultTenantIds.Primary,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var healed = await UserTenantMembershipHealSeed.HealLegacyDefaultAlongsideDemoTenantsCoreAsync(db);

        Assert.Equal(0, healed);
        Assert.True(await db.UserTenantMemberships.IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == "demo@demo.com" && m.IsActive));
    }

    [Fact]
    public async Task Heal_Is_Idempotent()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = LegacyDefaultTenantIds.Primary,
            Name = "Default",
            Slug = LegacyDefaultTenantIds.PrimarySlug,
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "cashier1",
            TenantId = LegacyDefaultTenantIds.Primary,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "cashier1",
            TenantId = DemoTenantIds.Dev,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.Equal(1, await UserTenantMembershipHealSeed.HealLegacyDefaultAlongsideDemoTenantsCoreAsync(db));
        Assert.Equal(0, await UserTenantMembershipHealSeed.HealLegacyDefaultAlongsideDemoTenantsCoreAsync(db));
    }
}
