using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminTenantsControllerTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminTenants_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static UserManager<ApplicationUser> CreateUserManagerStub()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new UserManager<ApplicationUser>(
            store.Object,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
    }

    private static AdminTenantService CreateService(AppDbContext db, ITenantProvisioningService? provisioning = null)
    {
        var provisioningMock = provisioning ?? CreateSuccessfulProvisioningMock();
        return new AdminTenantService(
            db,
            CreateUserManagerStub(),
            Mock.Of<ITokenClaimsService>(),
            Mock.Of<IRefreshTokenService>(),
            Mock.Of<IJwtAccessTokenIssuer>(),
            Options.Create(new AuthOptions()),
            provisioningMock,
            Mock.Of<ILogger<AdminTenantService>>());
    }

    private static ITenantProvisioningService CreateSuccessfulProvisioningMock()
    {
        var mock = new Mock<ITenantProvisioningService>();
        mock.Setup(p => p.ProvisionAsync(
                It.IsAny<Tenant>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant t, string? _, string? __, bool grantTrial, CancellationToken _) =>
            {
                if (grantTrial)
                    t.LicenseValidUntilUtc = DateTime.UtcNow.AddDays(30);
                return (new TenantProvisioningResult
                {
                    CashRegisterId = Guid.NewGuid(),
                    CashRegisterNumber = "KASSE-001",
                    AdminUserId = "admin-id",
                    AdminEmail = $"admin@{t.Slug}.regkasse.at",
                    GeneratedPassword = "TestPass1!",
                    CategoryId = Guid.NewGuid(),
                    ProductIds = new List<Guid> { Guid.NewGuid() },
                }, null);
            });
        return mock.Object;
    }

    [Fact]
    public async Task CreateAsync_PersistsTenant_WithSlug()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var (result, error) = await service.CreateAsync(
            new CreateAdminTenantRequest { Name = "Test Cafe", Slug = "test_cafe" },
            "actor-1");

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("test_cafe", result!.Slug);
        Assert.Equal(TenantStatuses.Active, result.Status);
        Assert.NotNull(result.Provisioning);
        Assert.Equal("KASSE-001", result.Provisioning!.CashRegisterNumber);
    }

    [Fact]
    public async Task SoftDeleteAsync_MarksDeleted()
    {
        await using var db = CreateDb();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Temp",
            Slug = "temp_tenant",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (success, error) = await service.SoftDeleteAsync(tenant.Id, "actor-1");

        Assert.True(success);
        Assert.Null(error);
        var row = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenant.Id);
        Assert.Equal(TenantStatuses.Deleted, row.Status);
        Assert.False(row.IsActive);
    }

    [Fact]
    public async Task SoftDeleteAsync_RejectsLegacyDefaultTenant()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = LegacyDefaultTenantIds.Primary,
            Name = "Default",
            Slug = LegacyDefaultTenantIds.PrimarySlug,
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var (success, error) = await service.SoftDeleteAsync(LegacyDefaultTenantIds.Primary, "actor-1");

        Assert.False(success);
        Assert.NotNull(error);
    }
}
