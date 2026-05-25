using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Auth;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AuthServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly DateTime Now = new(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"auth_service_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedMembershipAsync(AppDbContext db, DateTime? validUntilUtc)
    {
        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Locked Tenant",
            Slug = "locked-tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = validUntilUtc,
            CreatedAt = Now,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "u1",
            TenantId = TenantId,
            IsActive = true,
            CreatedAtUtc = Now,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ResolveLoginTenantAccessAsync_BlocksNormalUserDuringTenantLockdown()
    {
        await using var db = CreateDb();
        await SeedMembershipAsync(db, Now.AddDays(-120));

        var resolver = new Mock<ILoginTenantResolver>();
        resolver.Setup(x => x.ResolveSnapshotForLoginAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthTenantSnapshot(TenantId.ToString("D"), "Locked Tenant", "locked-tenant", null, null));

        var sut = new AuthService(db, resolver.Object);
        var result = await sut.ResolveLoginTenantAccessAsync("u1", isSuperAdmin: false, CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Equal(LoginTenantBlockedException.CodeTenantLicenseLockdown, result.Code);
    }

    [Fact]
    public async Task ResolveLoginTenantAccessAsync_AllowsSuperAdminDuringTenantLockdown()
    {
        await using var db = CreateDb();
        await SeedMembershipAsync(db, Now.AddDays(-120));

        var resolver = new Mock<ILoginTenantResolver>();
        resolver.Setup(x => x.ResolveSnapshotForLoginAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthTenantSnapshot(TenantId.ToString("D"), "Locked Tenant", "locked-tenant", null, null));

        var sut = new AuthService(db, resolver.Object);
        var result = await sut.ResolveLoginTenantAccessAsync("u1", isSuperAdmin: true, CancellationToken.None);

        Assert.True(result.Allowed);
        Assert.NotNull(result.Snapshot);
    }
}
