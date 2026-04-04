using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public class AuthTenantSnapshotProviderTests
{
    [Fact]
    public async Task GetSnapshotAsync_Without_Claim_Uses_Legacy_Default_And_Db_Name()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        db.Tenants.Add(new Tenant
        {
            Id = LegacyDefaultTenantIds.Primary,
            Name = "Seeded Org",
            Slug = LegacyDefaultTenantIds.PrimarySlug,
        });
        await db.SaveChangesAsync();

        var provider = new AuthTenantSnapshotProvider(db);
        var snap = await provider.GetSnapshotAsync(user: null);

        Assert.Equal(LegacyDefaultTenantIds.Primary.ToString("D"), snap.TenantId);
        Assert.Equal("Seeded Org", snap.TenantDisplayName);
        Assert.Null(snap.BranchId);
    }

    [Fact]
    public async Task GetSnapshotAsync_With_Valid_Claim_Uses_That_Tenant()
    {
        var otherId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        db.Tenants.Add(new Tenant { Id = LegacyDefaultTenantIds.Primary, Name = "Default", Slug = "default" });
        db.Tenants.Add(new Tenant { Id = otherId, Name = "Other", Slug = "other" });
        await db.SaveChangesAsync();

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ScopeCheckService.TenantIdClaim, otherId.ToString("D")),
        }));

        var provider = new AuthTenantSnapshotProvider(db);
        var snap = await provider.GetSnapshotAsync(user);

        Assert.Equal(otherId.ToString("D"), snap.TenantId);
        Assert.Equal("Other", snap.TenantDisplayName);
    }

    [Fact]
    public async Task GetSnapshotAsync_Invalid_Claim_Falls_Back_To_Default()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        db.Tenants.Add(new Tenant { Id = LegacyDefaultTenantIds.Primary, Name = "Default", Slug = "default" });
        await db.SaveChangesAsync();

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ScopeCheckService.TenantIdClaim, "not-a-guid"),
        }));

        var provider = new AuthTenantSnapshotProvider(db);
        var snap = await provider.GetSnapshotAsync(user);

        Assert.Equal(LegacyDefaultTenantIds.Primary.ToString("D"), snap.TenantId);
    }

    [Fact]
    public async Task ResolveForTokenIssuanceAsync_Persisted_Tenant_Wins_Over_Jwt_Claim()
    {
        var persistedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var claimId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        db.Tenants.Add(new Tenant { Id = persistedId, Name = "From Session", Slug = "from-session" });
        db.Tenants.Add(new Tenant { Id = claimId, Name = "From Jwt", Slug = "from-jwt" });
        await db.SaveChangesAsync();

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ScopeCheckService.TenantIdClaim, claimId.ToString("D")),
        }));

        var provider = new AuthTenantSnapshotProvider(db);
        var snap = await provider.ResolveForTokenIssuanceAsync(persistedId.ToString("D"), user);

        Assert.Equal(persistedId.ToString("D"), snap.TenantId);
        Assert.Equal("From Session", snap.TenantDisplayName);
    }

    [Fact]
    public async Task ResolveForTokenIssuanceAsync_Invalid_Persisted_Falls_Back_To_Valid_Claim()
    {
        var claimId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        db.Tenants.Add(new Tenant { Id = LegacyDefaultTenantIds.Primary, Name = "Default", Slug = "default" });
        db.Tenants.Add(new Tenant { Id = claimId, Name = "Claim Org", Slug = "claim-org" });
        await db.SaveChangesAsync();

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ScopeCheckService.TenantIdClaim, claimId.ToString("D")),
        }));

        var provider = new AuthTenantSnapshotProvider(db);
        var snap = await provider.ResolveForTokenIssuanceAsync("deadbeef-dead-beef-dead-beefdeadbeef", user);

        Assert.Equal(claimId.ToString("D"), snap.TenantId);
        Assert.Equal("Claim Org", snap.TenantDisplayName);
    }
}
