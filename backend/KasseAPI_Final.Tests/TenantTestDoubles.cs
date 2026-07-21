using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace KasseAPI_Final.Tests;

/// <summary>Shared test doubles for controllers that depend on tenant resolution / membership provisioning.</summary>
internal static class TenantTestDoubles
{
    /// <summary>Resolver fixed to <see cref="LegacyDefaultTenantIds.Primary"/> for legacy single-tenant test data.</summary>
    public static ISettingsTenantResolver PrimaryTenantResolver => SettingsResolverReturning(LegacyDefaultTenantIds.Primary);

    public static ISettingsTenantResolver SettingsResolverReturning(Guid tenantId)
    {
        var m = new Mock<ISettingsTenantResolver>();
        m.Setup(x => x.ResolveEffectiveTenantIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tenantId);
        return m.Object;
    }

    public static ICurrentTenantAccessor TenantAccessorReturning(Guid? tenantId) =>
        new MutableTenantAccessor(tenantId);

    /// <summary>In-memory test factory sharing one database name across created contexts.</summary>
    public static IDbContextFactory<AppDbContext> DbContextFactoryForTests(
        DbContextOptions<AppDbContext> options,
        ICurrentTenantAccessor? tenantAccessor = null) =>
        new TestAppDbContextFactory(options, tenantAccessor ?? TenantAccessorReturning(LegacyDefaultTenantIds.Primary));

    internal sealed class TestAppDbContextFactory(
        DbContextOptions<AppDbContext> options,
        ICurrentTenantAccessor tenantAccessor) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options, tenantAccessor);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    internal sealed class MutableTenantAccessor(Guid? tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
    }

    public static ICompanyProfileProvider CompanyProfileProviderReturning(CompanyProfileOptions profile)
    {
        var m = new Mock<ICompanyProfileProvider>();
        m.Setup(x => x.GetCompanyProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        return m.Object;
    }

    public static IHostEnvironment HostEnvironmentReturning(string environmentName)
    {
        var m = new Mock<IHostEnvironment>();
        m.Setup(x => x.EnvironmentName).Returns(environmentName);
        return m.Object;
    }

    public static IHostEnvironment ProductionHostEnvironment =>
        HostEnvironmentReturning(Environments.Production);

    public static ICashRegisterSettingsService CashRegisterSettingsServiceReturning(
        PosCashRegisterFeatureOptions features)
    {
        var m = new Mock<ICashRegisterSettingsService>();
        m.Setup(x => x.GetFeatureOptionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(features);
        return m.Object;
    }

    /// <summary>Inserts legacy primary tenant if missing (required for FK on tenant-scoped catalog rows in in-memory tests).</summary>
    public static void EnsureDefaultTenant(AppDbContext context)
    {
        if (!context.Tenants.AsNoTracking().Any(t => t.Id == LegacyDefaultTenantIds.Primary))
        {
            context.Tenants.Add(new Tenant
            {
                Id = LegacyDefaultTenantIds.Primary,
                Name = "Default",
                Slug = LegacyDefaultTenantIds.PrimarySlug
            });
        }
    }

    public static IUserTenantMembershipProvisioner NoOpProvisioner(Mock<IUserTenantMembershipProvisioner>? capture = null)
    {
        var m = capture ?? new Mock<IUserTenantMembershipProvisioner>();
        m.Setup(x => x.ProvisionActiveMembershipAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m.Object;
    }

    public static AdminPaymentsController CreateAdminPaymentsController(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        IPaymentService? paymentService = null,
        IAdminPaymentListService? paymentListService = null)
    {
        paymentListService ??= new AdminPaymentListService(
            db,
            tenantResolver,
            new PaymentMethodCatalogService(db, tenantResolver));

        var reversalOptions = new Mock<IOptionsMonitor<PaymentReversalApprovalOptions>>();
        reversalOptions.Setup(x => x.CurrentValue).Returns(new PaymentReversalApprovalOptions());

        return new AdminPaymentsController(
            db,
            paymentService ?? Mock.Of<IPaymentService>(),
            Mock.Of<IReceiptPdfService>(),
            paymentListService,
            Mock.Of<IAdminSuspiciousAlertService>(),
            Mock.Of<IPaymentTrendAnalysisService>(),
            NoOpPaymentReversalApprovalService.Instance,
            reversalOptions.Object,
            Mock.Of<ILogger<AdminPaymentsController>>(),
            tenantResolver);
    }
}
