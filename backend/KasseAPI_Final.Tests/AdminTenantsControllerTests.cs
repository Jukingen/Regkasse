using KasseAPI_Final.Authorization;
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
    public async Task CheckSlugAvailabilityAsync_ReturnsTakenWhenExists()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            Slug = "cafe-example",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CheckSlugAvailabilityAsync("cafe-example");

        Assert.True(result.IsValid);
        Assert.False(result.Available);
        Assert.Equal("cafe-example", result.NormalizedSlug);
    }

    [Fact]
    public async Task CheckSlugAvailabilityAsync_ReturnsAvailableForNewSlug()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        var result = await service.CheckSlugAvailabilityAsync("new-cafe");

        Assert.True(result.IsValid);
        Assert.True(result.Available);
        Assert.Equal("new-cafe", result.NormalizedSlug);
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
    public async Task ListAsync_Includes_Owner_Admin_And_Demo_Preset_Flags()
    {
        await using var db = CreateDb();
        var barId = DemoTenantIds.Bar;
        var cafeId = DemoTenantIds.Cafe;
        db.Tenants.Add(new Tenant
        {
            Id = barId,
            Name = "Test Bar",
            Slug = "bar",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = cafeId,
            Name = "Café Beispiel",
            Slug = "cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "owner-bar",
            TenantId = barId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = "owner-bar",
            UserName = "admin@bar.regkasse.at",
            Email = "admin@bar.regkasse.at",
            FirstName = "A",
            LastName = "B",
            Role = Roles.Manager,
            IsActive = true,
            EmailConfirmed = true,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var list = await service.ListAsync(false);

        var bar = list.Single(x => x.Slug == "bar");
        Assert.Equal("admin@bar.regkasse.at", bar.OwnerAdminEmail);
        Assert.False(bar.IsDemoPreset);

        var cafe = list.Single(x => x.Slug == "cafe");
        Assert.Null(cafe.OwnerAdminEmail);
        Assert.False(cafe.IsDemoPreset);

        var dev = list.Single(x => x.Slug == "dev");
        Assert.True(dev.IsDemoPreset);
    }

    [Fact]
    public async Task ListForSwitcherAsync_Filters_To_Active_Memberships_For_Non_SuperAdmin()
    {
        await using var db = CreateDb();
        var memberTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = memberTenantId,
            Name = "Member Cafe",
            Slug = "member-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = otherTenantId,
            Name = "Other Bar",
            Slug = "other-bar",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "manager-1",
            TenantId = memberTenantId,
            IsActive = true,
            IsOwner = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var all = await service.ListForSwitcherAsync("manager-1", actorIsSuperAdmin: false, includeDeleted: false);
        Assert.Single(all);
        Assert.Equal("member-cafe", all[0].Slug);

        var superList = await service.ListForSwitcherAsync("manager-1", actorIsSuperAdmin: true, includeDeleted: false);
        Assert.Equal(2, superList.Count);
    }

    [Fact]
    public async Task ListCashRegistersAsync_ReturnsRegisters_ForTenant()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe-x",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Hauptkasse",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var registers = await service.ListCashRegistersAsync(tenantId);

        Assert.NotNull(registers);
        Assert.Single(registers!);
        Assert.Equal("KASSE-001", registers![0].RegisterNumber);
        Assert.Equal("Open", registers[0].Status);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesSummaryCounts()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Stats",
            Slug = "stats",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = "u1",
            TenantId = tenantId,
            IsActive = true,
            IsOwner = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Main",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var detail = await service.GetByIdAsync(tenantId);

        Assert.NotNull(detail);
        Assert.Equal(1, detail!.ActiveUserCount);
        Assert.Equal(1, detail.CashRegisterCount);
        Assert.NotNull(detail.LastActivityAtUtc);
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
