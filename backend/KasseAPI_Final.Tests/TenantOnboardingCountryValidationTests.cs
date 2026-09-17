using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Services.Trial;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>Paket 4: country + VAT regime validation and CompanySettings seed at tenant create.</summary>
public sealed class TenantOnboardingCountryValidationTests
{
    private static readonly ICountryProfileRegistry Registry = new CountryProfileRegistry();

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"country_onboarding_{Guid.NewGuid():N}")
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
        mgr.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        return mgr;
    }

    private static ITrialService CreateTrialServiceStub()
    {
        var trial = new Mock<ITrialService>();
        trial.Setup(x => x.ResolveDurationDays(It.IsAny<int?>())).Returns(14);
        trial.Setup(x => x.ApplyTrialGrant(It.IsAny<Tenant>(), It.IsAny<int>(), It.IsAny<DateTime>()))
            .Callback<Tenant, int, DateTime>((tenant, days, now) =>
            {
                tenant.TrialStartedAtUtc = now;
                tenant.TrialEndsAtUtc = now.AddDays(days);
                tenant.TrialStatus = TrialStatuses.Active;
                tenant.LicenseValidUntilUtc = tenant.TrialEndsAtUtc;
                tenant.UpdatedAt = now;
            });
        return trial.Object;
    }

    private static TenantOnboardingService CreateOnboarding(
        AppDbContext db,
        ITseProvisioningService? tse = null,
        IAuditLogService? audit = null)
    {
        var uniqueness = new Mock<IUserUniquenessValidationService>();
        uniqueness.Setup(x => x.IsEmailTakenByOtherUserAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(false);

        var provisioning = new TenantProvisioningService(
            db,
            CreateUserManagerMock().Object,
            new UserTenantMembershipProvisioner(db),
            uniqueness.Object,
            Mock.Of<IDemoProductImportService>(),
            new PaymentMethodDefinitionBootstrapService(db),
            tse ?? TseProvisioningTestDoubles.Successful(),
            CreateTrialServiceStub(),
            Mock.Of<ILogger<TenantProvisioningService>>());

        var checklist = new Mock<KasseAPI_Final.Services.Onboarding.ITenantOnboardingChecklistService>();
        checklist
            .Setup(c => c.EnsureAndGetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KasseAPI_Final.Services.Onboarding.TenantOnboardingOverviewDto());

        return new TenantOnboardingService(
            db,
            provisioning,
            Mock.Of<IWelcomeEmailService>(),
            audit ?? Mock.Of<IAuditLogService>(),
            checklist.Object,
            Registry,
            Mock.Of<ILogger<TenantOnboardingService>>());
    }

    private static AdminTenantsController CreateController(IAdminTenantService tenants)
    {
        var controller = new AdminTenantsController(
            tenants,
            Mock.Of<IAdminTenantCsvExportService>(),
            Mock.Of<IAdminTenantLicenseService>(),
            Mock.Of<ITenantDeletionService>(),
            Mock.Of<KasseAPI_Final.Services.ActivityReports.IActivityReportService>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<KasseAPI_Final.Services.Trial.ITrialConversionService>(),
            Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development),
            Mock.Of<ILogger<AdminTenantsController>>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, "super-admin"),
                            new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                        ],
                        authenticationType: "TestAuth"))
            }
        };
        return controller;
    }

    private static CreateAdminTenantRequest AtRequest(string slug, string name) => new()
    {
        Name = name,
        Slug = slug,
        Email = $"info@{slug}.at",
        AdminEmail = $"admin@{slug}.at",
        CountryCode = "AT",
        VatRegime = VatRegime.AT_RKSV_STANDARD,
        GrantTrialLicense = false,
        ImportDemoMenu = false,
    };

    [Fact]
    public async Task CreateAsync_UnknownCountry_ThrowsUnknownCountryCode_AndDoesNotInsert()
    {
        await using var db = CreateDb();
        var onboarding = CreateOnboarding(db);
        var before = await db.Tenants.CountAsync();

        var request = AtRequest("unknown-land", "Unknown Land");
        request.CountryCode = "XX";

        var ex = await Assert.ThrowsAsync<UnknownCountryCodeException>(
            () => onboarding.CreateAsync(request, "super-admin-1"));

        Assert.Equal(UnknownCountryCodeException.Code, ex.ErrorCode);
        Assert.Equal(before, await db.Tenants.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_EuDefault_ThrowsCountryNotSelectable_AndDoesNotInsert()
    {
        await using var db = CreateDb();
        var onboarding = CreateOnboarding(db);
        var before = await db.Tenants.CountAsync();

        var request = AtRequest("eu-default-cafe", "EU Default Cafe");
        request.CountryCode = CountryProfileCodes.EuDefault;
        request.VatRegime = VatRegime.EU_REVERSE_CHARGE;

        var ex = await Assert.ThrowsAsync<CountryNotSelectableException>(
            () => onboarding.CreateAsync(request, "super-admin-1"));

        Assert.Equal(CountryNotSelectableException.Code, ex.ErrorCode);
        Assert.Equal(before, await db.Tenants.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_InvalidVatRegime_ThrowsInvalidVatRegimeForCountry_AndDoesNotInsert()
    {
        await using var db = CreateDb();
        var onboarding = CreateOnboarding(db);
        var before = await db.Tenants.CountAsync();

        var request = AtRequest("bad-regime", "Bad Regime Cafe");
        request.VatRegime = VatRegime.DE_USTG_STANDARD;

        var ex = await Assert.ThrowsAsync<InvalidVatRegimeForCountryException>(
            () => onboarding.CreateAsync(request, "super-admin-1"));

        Assert.Equal(InvalidVatRegimeForCountryException.Code, ex.ErrorCode);
        Assert.Equal(before, await db.Tenants.CountAsync());
    }

    [Fact]
    public async Task Create_UnknownCountry_Returns400UnknownCountryCode()
    {
        var result = await InvokeCreateThrowing(new UnknownCountryCodeException("XX"));
        AssertBadRequestCode(result, UnknownCountryCodeException.Code);
    }

    [Fact]
    public async Task Create_EuDefault_Returns400CountryNotSelectable()
    {
        var result = await InvokeCreateThrowing(new CountryNotSelectableException(CountryProfileCodes.EuDefault));
        AssertBadRequestCode(result, CountryNotSelectableException.Code);
    }

    [Fact]
    public async Task Create_InvalidVatRegime_Returns400InvalidVatRegimeForCountry()
    {
        var result = await InvokeCreateThrowing(
            new InvalidVatRegimeForCountryException("AT", VatRegime.DE_USTG_STANDARD));
        AssertBadRequestCode(result, InvalidVatRegimeForCountryException.Code);
    }

    [Fact]
    public async Task CreateAsync_Germany_SeedsCountryVatRegimeAndCurrency()
    {
        await using var db = CreateDb();
        var tse = new Mock<ITseProvisioningService>();
        var onboarding = CreateOnboarding(db, TseProvisioningTestDoubles.Successful(tse));
        var de = Registry.Get(CountryProfileCodes.Germany);

        var (result, failure) = await onboarding.CreateAsync(
            new CreateAdminTenantRequest
            {
                Name = "Berlin Cafe",
                Slug = "berlin-cafe",
                Email = "info@berlin-cafe.de",
                AdminEmail = "admin@berlin-cafe.de",
                CountryCode = "DE",
                VatRegime = VatRegime.DE_USTG_STANDARD,
                GrantTrialLicense = false,
                ImportDemoMenu = false,
            },
            "super-admin-1");

        Assert.Null(failure);
        Assert.NotNull(result);

        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Slug == "berlin-cafe");
        var company = await db.CompanySettings.IgnoreQueryFilters()
            .SingleAsync(s => s.TenantId == tenant.Id);

        Assert.Equal("DE", company.Country);
        Assert.Equal(VatRegime.DE_USTG_STANDARD, company.VatRegime);
        Assert.Equal("EUR", company.Currency);
        Assert.Equal(de.DefaultLocale, company.Language);
        Assert.Equal(de.DefaultTimeZone, company.TimeZone);

        Assert.False(result!.Provisioning!.TseProvisioned);
        tse.Verify(
            x => x.ProvisionTseForCashRegisterAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Empty(await db.TaxGroups.IgnoreQueryFilters().Where(g => g.TenantId == tenant.Id).ToListAsync());
        Assert.Empty(await db.Products.IgnoreQueryFilters().Where(p => p.TenantId == tenant.Id).ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_Austria_KeepsExistingProvisioning_AndAddsCompanySettingsFromAtSeed()
    {
        await using var db = CreateDb();
        var tse = new Mock<ITseProvisioningService>();
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<KasseAPI_Final.Services.ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var onboarding = CreateOnboarding(db, TseProvisioningTestDoubles.Successful(tse), audit.Object);
        var at = Registry.Default;

        var (result, failure) = await onboarding.CreateAsync(
            AtRequest("at-country-seed", "AT Country Seed"),
            "super-admin-1");

        Assert.Null(failure);
        Assert.NotNull(result);
        Assert.Equal("KASSE-001", result!.Provisioning!.CashRegisterNumber);
        Assert.Equal(3, result.Provisioning.ProductIds.Count);
        Assert.True(result.Provisioning.TseProvisioned);

        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Slug == "at-country-seed");
        Assert.Equal(1, await db.CashRegisters.IgnoreQueryFilters().CountAsync(r => r.TenantId == tenant.Id));
        Assert.Equal(3, await db.Products.IgnoreQueryFilters().CountAsync(p => p.TenantId == tenant.Id));
        Assert.True(await db.TaxGroups.IgnoreQueryFilters().AnyAsync(g => g.TenantId == tenant.Id));

        var company = await db.CompanySettings.IgnoreQueryFilters()
            .SingleAsync(s => s.TenantId == tenant.Id);
        Assert.Equal(at.Code, company.Country);
        Assert.Equal(VatRegime.AT_RKSV_STANDARD, company.VatRegime);
        Assert.Equal(at.Currency, company.Currency);
        Assert.Equal(at.DefaultLocale, company.Language);
        Assert.Equal(at.DefaultTimeZone, company.TimeZone);

        tse.Verify(
            x => x.ProvisionTseForCashRegisterAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);

        audit.Verify(
            a => a.LogSystemOperationAsync(
                "TENANT_CREATED_WITH_COUNTRY",
                "Tenant",
                "super-admin-1",
                Roles.SuperAdmin,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<KasseAPI_Final.Services.ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.TenantCreatedWithCountry,
                tenant.Id,
                tenant.Id,
                It.IsAny<object?>(),
                It.Is<object>(v => JsonSerializer.Serialize(v).Contains("AT", StringComparison.Ordinal)
                    && JsonSerializer.Serialize(v).Contains("AT_RKSV_STANDARD", StringComparison.Ordinal)),
                It.IsAny<string?>()),
            Times.Once);
    }

    private static async Task<ActionResult<AdminTenantDetailDto>> InvokeCreateThrowing(Exception exception)
    {
        var tenants = new Mock<IAdminTenantService>();
        tenants
            .Setup(s => s.CreateWithFailureDetailAsync(
                It.IsAny<CreateAdminTenantRequest>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var controller = CreateController(tenants.Object);
        return await controller.Create(AtRequest("mapped", "Mapped"));
    }

    private static void AssertBadRequestCode(ActionResult<AdminTenantDetailDto> result, string code)
    {
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var json = JsonSerializer.Serialize(bad.Value);
        Assert.Contains(code, json, StringComparison.Ordinal);
    }
}
