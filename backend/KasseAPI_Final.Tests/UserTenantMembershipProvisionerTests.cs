using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public class UserTenantMembershipProvisionerTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"utm_provisioner_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ProvisionActiveMembershipAsync_Creates_Row_For_New_User()
    {
        await using var db = CreateDb();
        var tid = LegacyDefaultTenantIds.Primary;
        db.Tenants.Add(new Tenant { Id = tid, Name = "T", Slug = "t" });
        await db.SaveChangesAsync();

        var p = new UserTenantMembershipProvisioner(db);
        await p.ProvisionActiveMembershipAsync("user-1", tid);

        var m = await db.UserTenantMemberships.SingleAsync();
        Assert.Equal("user-1", m.UserId);
        Assert.Equal(tid, m.TenantId);
        Assert.True(m.IsActive);
    }

    [Fact]
    public async Task ProvisionActiveMembershipAsync_Idempotent_When_Already_Active_Same_Tenant()
    {
        await using var db = CreateDb();
        var tid = LegacyDefaultTenantIds.Primary;
        db.Tenants.Add(new Tenant { Id = tid, Name = "T", Slug = "t" });
        await db.SaveChangesAsync();

        var p = new UserTenantMembershipProvisioner(db);
        await p.ProvisionActiveMembershipAsync("user-1", tid);
        await p.ProvisionActiveMembershipAsync("user-1", tid);

        Assert.Equal(1, await db.UserTenantMemberships.CountAsync());
    }

    [Fact]
    public async Task ProvisionActiveMembershipAsync_Deactivates_Other_Active_When_Switching_Tenant()
    {
        await using var db = CreateDb();
        var t1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var t2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        db.Tenants.Add(new Tenant { Id = t1, Name = "A", Slug = "a" });
        db.Tenants.Add(new Tenant { Id = t2, Name = "B", Slug = "b" });
        await db.SaveChangesAsync();

        var p = new UserTenantMembershipProvisioner(db);
        await p.ProvisionActiveMembershipAsync("user-1", t1);
        await p.ProvisionActiveMembershipAsync("user-1", t2);

        var actives = await db.UserTenantMemberships.Where(x => x.UserId == "user-1" && x.IsActive).ToListAsync();
        Assert.Single(actives);
        Assert.Equal(t2, actives[0].TenantId);
    }

    [Fact]
    public async Task ProvisionActiveMembershipAsync_Throws_When_Tenant_Missing()
    {
        await using var db = CreateDb();
        var p = new UserTenantMembershipProvisioner(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            p.ProvisionActiveMembershipAsync("user-1", LegacyDefaultTenantIds.Primary));
    }
}
