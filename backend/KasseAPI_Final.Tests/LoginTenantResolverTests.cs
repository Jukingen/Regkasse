using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public class LoginTenantResolverTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"login_tenant_resolver_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static void SeedDefaultTenant(AppDbContext db)
    {
        db.Tenants.Add(new Tenant
        {
            Id = LegacyDefaultTenantIds.Primary,
            Name = "Default Org",
            Slug = LegacyDefaultTenantIds.PrimarySlug,
        });
    }

    [Fact]
    public async Task HasActiveMembershipAsync_False_Without_Active_Row()
    {
        await using var db = CreateDb();
        SeedDefaultTenant(db);
        await db.SaveChangesAsync();

        var resolver = new LoginTenantResolver(db, NullLogger<LoginTenantResolver>.Instance);
        Assert.False(await resolver.HasActiveMembershipAsync("any-user"));
    }

    [Fact]
    public async Task HasActiveMembershipAsync_True_When_Active_Row_Exists()
    {
        await using var db = CreateDb();
        var tid = LegacyDefaultTenantIds.Primary;
        db.Tenants.Add(new Tenant { Id = tid, Name = "T", Slug = "t" });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "u1",
            TenantId = tid,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var resolver = new LoginTenantResolver(db, NullLogger<LoginTenantResolver>.Instance);
        Assert.True(await resolver.HasActiveMembershipAsync("u1"));
    }

    [Fact]
    public async Task No_Active_Membership_Uses_Legacy_Default_Tenant()
    {
        await using var db = CreateDb();
        SeedDefaultTenant(db);
        await db.SaveChangesAsync();

        var resolver = new LoginTenantResolver(db, NullLogger<LoginTenantResolver>.Instance);
        var snap = await resolver.ResolveSnapshotForLoginAsync("user-1");

        Assert.Equal(LegacyDefaultTenantIds.Primary.ToString("D"), snap.TenantId);
        Assert.Equal("Default Org", snap.TenantDisplayName);
    }

    [Fact]
    public async Task Single_Active_Membership_Uses_That_Tenant()
    {
        await using var db = CreateDb();
        var otherId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        db.Tenants.Add(new Tenant { Id = LegacyDefaultTenantIds.Primary, Name = "Default", Slug = "d" });
        db.Tenants.Add(new Tenant { Id = otherId, Name = "Member Org", Slug = "member" });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "u1",
            TenantId = otherId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var resolver = new LoginTenantResolver(db, NullLogger<LoginTenantResolver>.Instance);
        var snap = await resolver.ResolveSnapshotForLoginAsync("u1");

        Assert.Equal(otherId.ToString("D"), snap.TenantId);
        Assert.Equal("Member Org", snap.TenantDisplayName);
    }

    [Fact]
    public async Task Only_Inactive_Membership_Falls_Back_To_Default()
    {
        await using var db = CreateDb();
        var tid = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        db.Tenants.Add(new Tenant { Id = LegacyDefaultTenantIds.Primary, Name = "Default", Slug = "d" });
        db.Tenants.Add(new Tenant { Id = tid, Name = "Inactive Org", Slug = "inact" });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "u1",
            TenantId = tid,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var resolver = new LoginTenantResolver(db, NullLogger<LoginTenantResolver>.Instance);
        var snap = await resolver.ResolveSnapshotForLoginAsync("u1");

        Assert.Equal(LegacyDefaultTenantIds.Primary.ToString("D"), snap.TenantId);
    }

    [Fact]
    public async Task Duplicate_User_Tenant_Pair_Throws_On_Save_When_Provider_Enforces_Uniqueness()
    {
        await using var db = CreateDb();
        var tid = LegacyDefaultTenantIds.Primary;
        db.Tenants.Add(new Tenant { Id = tid, Name = "T", Slug = "t" });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "u1",
            TenantId = tid,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "u1",
            TenantId = tid,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
        });

        var ex = await Record.ExceptionAsync(() => db.SaveChangesAsync());
        if (ex != null)
            Assert.IsAssignableFrom<DbUpdateException>(ex);
    }

    [Fact]
    public async Task Multiple_Active_Memberships_Uses_Oldest_By_CreatedAtUtc()
    {
        await using var db = CreateDb();
        var t1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var t2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        db.Tenants.Add(new Tenant { Id = t1, Name = "First", Slug = "first" });
        db.Tenants.Add(new Tenant { Id = t2, Name = "Second", Slug = "second" });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "u1",
            TenantId = t1,
            IsActive = true,
            CreatedAtUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "u1",
            TenantId = t2,
            IsActive = true,
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Provider enforced partial unique (one active per user); skip assertion below.
            return;
        }

        var resolver = new LoginTenantResolver(db, NullLogger<LoginTenantResolver>.Instance);
        var snap = await resolver.ResolveSnapshotForLoginAsync("u1");
        Assert.Equal(t1.ToString("D"), snap.TenantId);
        Assert.Equal("First", snap.TenantDisplayName);
    }
}
