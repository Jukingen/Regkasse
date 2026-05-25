using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantProvisioningServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tenant_provision_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object,
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());

        mgr.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        mgr.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) => u.Id = Guid.NewGuid().ToString("D"));
        mgr.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), Roles.Manager))
            .ReturnsAsync(IdentityResult.Success);

        return mgr;
    }

    [Fact]
    public void GenerateCompliantPassword_MeetsIdentityRules()
    {
        var password = TenantProvisioningService.GenerateCompliantPassword();
        Assert.True(password.Length >= 8);
        Assert.Contains(password, c => char.IsLower(c));
        Assert.Contains(password, c => char.IsUpper(c));
        Assert.Contains(password, c => char.IsDigit(c));
        Assert.Contains(password, c => !char.IsLetterOrDigit(c));
    }

    [Fact]
    public async Task ProvisionAsync_CreatesRegisterCategoryProductsAndMembership()
    {
        await using var db = CreateDb();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Cafe Demo",
            Slug = "cafe_demo",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var uniqueness = new Mock<IUserUniquenessValidationService>();
        uniqueness.Setup(x => x.IsEmailTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(false);

        var service = new TenantProvisioningService(
            db,
            CreateUserManagerMock().Object,
            new UserTenantMembershipProvisioner(db),
            uniqueness.Object,
            Mock.Of<ILogger<TenantProvisioningService>>());

        var (result, error) = await service.ProvisionAsync(tenant, null, null, grantTrialLicense: true);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("KASSE-001", result!.CashRegisterNumber);
        Assert.Equal("admin@cafe_demo.regkasse.at", result.AdminEmail);
        Assert.Equal(3, result.ProductIds.Count);

        var register = await db.CashRegisters.SingleAsync(r => r.TenantId == tenant.Id);
        Assert.Equal(RegisterStatus.Closed, register.Status);
        Assert.Equal("Hauptkasse", register.Location);

        var category = await db.Categories.SingleAsync(c => c.TenantId == tenant.Id);
        Assert.Equal("Allgemein", category.Name);

        var products = await db.Products.Where(p => p.TenantId == tenant.Id).ToListAsync();
        Assert.Equal(3, products.Count);
        Assert.All(products, p => Assert.False(string.IsNullOrWhiteSpace(p.Description)));

        var membership = await db.UserTenantMemberships.SingleAsync(m => m.TenantId == tenant.Id);
        Assert.True(membership.IsActive);
        Assert.True(membership.IsOwner);
        Assert.Equal(result.AdminUserId, membership.UserId);

        var settings = await db.UserSettings.SingleAsync(s => s.UserId == result.AdminUserId);
        Assert.Equal(register.Id.ToString("D"), settings.CashRegisterId);

        var reloadedTenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenant.Id);
        Assert.NotNull(reloadedTenant.LicenseValidUntilUtc);
        Assert.NotNull(result.TrialLicenseValidUntilUtc);
    }
}
