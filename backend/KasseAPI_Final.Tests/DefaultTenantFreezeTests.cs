using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DefaultTenantFreezeTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"default_freeze_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static UserTenantMembershipProvisioner CreateProvisioner(AppDbContext db) =>
        new(db, NullLogger<UserTenantMembershipProvisioner>.Instance);

    private static IQueryable<UserTenantMembership> Memberships(AppDbContext db) =>
        db.UserTenantMemberships.IgnoreQueryFilters();

    [Fact]
    public async Task ProvisionActiveMembershipAsync_Redirects_Platform_To_Dev()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = SystemTenantIds.Platform,
            Name = "Platform",
            Slug = SystemTenantIds.PlatformSlug,
            IsActive = false,
        });
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var provisioner = CreateProvisioner(db);
        await provisioner.ProvisionActiveMembershipAsync("user-1", SystemTenantIds.Platform);

        var membership = await Memberships(db).SingleAsync();
        Assert.Equal("user-1", membership.UserId);
        Assert.Equal(DemoTenantIds.Dev, membership.TenantId);
        Assert.True(membership.IsActive);
        Assert.Equal(0, await Memberships(db).CountAsync(m => m.TenantId == SystemTenantIds.Platform));
    }

    [Fact]
    public async Task ProvisionActiveMembershipAsync_Throws_When_Platform_And_Dev_Missing()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = SystemTenantIds.Platform,
            Name = "Platform",
            Slug = SystemTenantIds.PlatformSlug,
            IsActive = false,
        });
        await db.SaveChangesAsync();

        var provisioner = CreateProvisioner(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provisioner.ProvisionActiveMembershipAsync("user-1", SystemTenantIds.Platform));

        Assert.Contains("dev", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await Memberships(db).CountAsync());
    }

    [Fact]
    public async Task ProvisionActiveMembershipAsync_Allows_NonPlatform_Tenant()
    {
        await using var db = CreateDb();
        var businessId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        db.Tenants.Add(new Tenant { Id = businessId, Name = "Biz", Slug = "biz", IsActive = true });
        await db.SaveChangesAsync();

        var provisioner = CreateProvisioner(db);
        await provisioner.ProvisionActiveMembershipAsync("user-1", businessId);

        var membership = await Memberships(db).SingleAsync();
        Assert.Equal(businessId, membership.TenantId);
    }
}

