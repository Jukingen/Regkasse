using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// End-to-end onboarding scenarios for the Super Admin create-tenant wizard.
/// </summary>
public sealed class CreateTenantWizardScenarioTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"wizard_scenarios_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
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

    private static TenantOnboardingService CreateOnboarding(AppDbContext db, UserManager<ApplicationUser>? userManager = null)
    {
        var uniqueness = new Mock<IUserUniquenessValidationService>();
        uniqueness.Setup(x => x.IsEmailTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(false);

        var provisioning = new TenantProvisioningService(
            db,
            userManager ?? CreateUserManagerMock().Object,
            new UserTenantMembershipProvisioner(db),
            uniqueness.Object,
            Mock.Of<IDemoProductImportService>(),
            new PaymentMethodDefinitionBootstrapService(db),
            Mock.Of<ILogger<TenantProvisioningService>>());

        return new TenantOnboardingService(
            db,
            provisioning,
            Mock.Of<IWelcomeEmailService>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILogger<TenantOnboardingService>>());
    }

    [Fact]
    public async Task Scenario1_SuccessfulCreation_CafeMuster()
    {
        await using var db = CreateDb();
        var onboarding = CreateOnboarding(db);
        var licenseUntil = DateTime.UtcNow.Date.AddDays(365);

        var (result, failure) = await onboarding.CreateAsync(
            new CreateAdminTenantRequest
            {
                Name = "Cafe Muster GmbH",
                Slug = "cafe-muster",
                Email = "info@cafe-muster.at",
                AdminEmail = "admin@cafe-muster.at",
                GrantTrialLicense = true,
                LicenseValidUntilUtc = licenseUntil,
                ImportDemoMenu = false,
                CashRegisterNumber = "KASSE-001",
            },
            "super-admin-1");

        Assert.Null(failure);
        Assert.NotNull(result);
        Assert.Equal("Cafe Muster GmbH", result!.Name);
        Assert.Equal("cafe-muster", result.Slug);
        Assert.Equal("info@cafe-muster.at", result.Email);
        Assert.Equal(TenantStatuses.Active, result.Status);

        Assert.NotNull(result.Provisioning);
        Assert.Equal("admin@cafe-muster.at", result.Provisioning!.AdminEmail);
        Assert.False(string.IsNullOrWhiteSpace(result.Provisioning.GeneratedPassword));
        Assert.True(result.Provisioning.GeneratedPassword.Length >= 8);
        Assert.Equal("KASSE-001", result.Provisioning.CashRegisterNumber);
        Assert.Equal(3, result.Provisioning.ProductIds.Count);

        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Slug == "cafe-muster");
        Assert.Equal(licenseUntil, tenant.LicenseValidUntilUtc);

        var register = await db.CashRegisters.IgnoreQueryFilters()
            .SingleAsync(r => r.TenantId == tenant.Id);
        Assert.Equal("KASSE-001", register.RegisterNumber);

        var products = await db.Products.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenant.Id)
            .ToListAsync();
        Assert.Equal(3, products.Count);

        var membership = await db.UserTenantMemberships.IgnoreQueryFilters()
            .SingleAsync(m => m.TenantId == tenant.Id);
        Assert.True(membership.IsOwner);
        Assert.True(membership.IsActive);
    }

    [Fact]
    public async Task Scenario2_DuplicateSlug_ReturnsSlugTaken_AndDoesNotCreate()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Existing Cafe Muster",
            Slug = "cafe-muster",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var onboarding = CreateOnboarding(db);
        var beforeCount = await db.Tenants.CountAsync();

        var (result, failure) = await onboarding.CreateAsync(
            new CreateAdminTenantRequest
            {
                Name = "Test Cafe",
                Slug = "cafe-muster",
                Email = "test@cafe.at",
                AdminEmail = "admin@cafe.at",
            },
            "super-admin-1");

        Assert.Null(result);
        Assert.NotNull(failure);
        Assert.Equal(TenantOnboardingErrorCodes.SlugTaken, failure!.Code);
        Assert.Contains("cafe-muster", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(failure.SlugSuggestions ?? Array.Empty<string>());
        Assert.Equal(beforeCount, await db.Tenants.CountAsync());
    }

    [Fact]
    public async Task Scenario2_SlugAvailability_ReportsTakenForCafeMuster()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            Slug = "cafe-muster",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Mirrors GET /api/admin/tenants/slug-availability?slug=cafe-muster
        var normalized = TenantSlugSuggestions.NormalizeSlug("cafe-muster");
        var available = TenantSlugSuggestions.IsValidSlug(normalized)
            && !await db.Tenants.AnyAsync(t => t.Slug == normalized);

        Assert.Equal("cafe-muster", normalized);
        Assert.False(available);
    }

    [Fact]
    public async Task Scenario4_WeakPassword_Rejected_AndRolledBack()
    {
        await using var db = CreateDb();
        var onboarding = CreateOnboarding(db);

        var (result, failure) = await onboarding.CreateAsync(
            new CreateAdminTenantRequest
            {
                Name = "Weak Pass Cafe",
                Slug = "weak-pass-cafe",
                Email = "info@weak.at",
                AdminEmail = "admin@weak.at",
                AdminPassword = "123",
            },
            "super-admin-1");

        Assert.Null(result);
        Assert.NotNull(failure);
        Assert.Equal(TenantOnboardingErrorCodes.ProvisioningFailed, failure!.Code);
        Assert.Contains("8", failure.Message, StringComparison.Ordinal);

        // EF InMemory ignores transactions, so the tenant row may remain; production rolls back.
        // Assert provisioning side effects were not committed.
        var orphan = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == "weak-pass-cafe");
        if (orphan != null)
        {
            Assert.False(await db.CashRegisters.IgnoreQueryFilters().AnyAsync(r => r.TenantId == orphan.Id));
            Assert.False(await db.UserTenantMemberships.IgnoreQueryFilters().AnyAsync(m => m.TenantId == orphan.Id));
            Assert.False(await db.Products.IgnoreQueryFilters().AnyAsync(p => p.TenantId == orphan.Id));
        }
    }
}
