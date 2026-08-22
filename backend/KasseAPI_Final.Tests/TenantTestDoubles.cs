using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace KasseAPI_Final.Tests;

/// <summary>Shared test doubles for controllers that depend on tenant resolution / membership provisioning.</summary>
internal static class TenantTestDoubles
{
    /// <summary>Resolver fixed to <see cref="SystemTenantIds.Platform"/> for platform-sentinel test data.</summary>
    public static ISettingsTenantResolver PrimaryTenantResolver => SettingsResolverReturning(SystemTenantIds.Platform);

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
        new TestAppDbContextFactory(options, tenantAccessor ?? TenantAccessorReturning(SystemTenantIds.Platform));

    internal sealed class TestAppDbContextFactory(
        DbContextOptions<AppDbContext> options,
        ICurrentTenantAccessor tenantAccessor) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options, tenantAccessor);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    internal sealed class MutableTenantAccessor(Guid? tenantId, string? tenantSlug = null) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; } = tenantSlug;
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

    /// <summary>Inserts platform sentinel tenant if missing (required for FK on tenant-scoped catalog rows in in-memory tests).</summary>
    public static void EnsurePlatformTenant(AppDbContext context)
    {
        // AsNoTracking() only sees saved rows, so a second call before SaveChanges would add a duplicate.
        var pending = context.ChangeTracker.Entries<Tenant>()
            .Any(e => e.Entity.Id == SystemTenantIds.Platform);
        if (pending || context.Tenants.AsNoTracking().Any(t => t.Id == SystemTenantIds.Platform))
            return;

        context.Tenants.Add(new Tenant
        {
            Id = SystemTenantIds.Platform,
            Name = "Platform",
            Slug = SystemTenantIds.PlatformSlug
        });
    }

    /// <summary>
    /// Inserts a throwaway tenant if missing. PostgreSQL integration tests share one database across the collection,
    /// so giving each test its own tenant keeps the per-tenant unique indexes (register number, category key, …) apart.
    /// </summary>
    public static void EnsureTenant(AppDbContext context, Guid tenantId, string? slug = null)
    {
        if (tenantId == SystemTenantIds.Platform)
        {
            EnsurePlatformTenant(context);
            return;
        }

        var pending = context.ChangeTracker.Entries<Tenant>().Any(e => e.Entity.Id == tenantId);
        if (pending || context.Tenants.IgnoreQueryFilters().AsNoTracking().Any(t => t.Id == tenantId))
            return;

        var effectiveSlug = slug ?? $"t-{tenantId:N}"[..12];
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = effectiveSlug,
            Slug = effectiveSlug
        });
    }

    /// <summary>
    /// Inserts a tenant tax group and returns its id. <see cref="Product.TaxGroupId"/> is a required FK, so products
    /// seeded without one are rows PostgreSQL would reject — and the admin list projection (inner join) silently drops
    /// them on the in-memory provider.
    /// </summary>
    public static Guid EnsureTaxGroup(AppDbContext context, Guid tenantId, decimal rate = 10m)
    {
        var existing = context.ChangeTracker.Entries<TaxGroup>()
            .Select(e => e.Entity)
            .FirstOrDefault(g => g.TenantId == tenantId && g.Rate == rate)
            ?? context.TaxGroups.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefault(g => g.TenantId == tenantId && g.Rate == rate);
        if (existing != null)
            return existing.Id;

        var group = new TaxGroup
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"MwSt {rate:0.##}%",
            Rate = rate,
            IsSystem = true,
        };
        context.TaxGroups.Add(group);
        return group.Id;
    }

    /// <summary>
    /// SuperAdmin HTTP principal so tenant-user APIs that call <c>CanAccessTenant</c> do not 404 as "Tenant not found".
    /// </summary>
    public static IHttpContextAccessor SuperAdminHttpAccessor(string userId = "test-super-admin")
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                    new Claim(ClaimTypes.NameIdentifier, userId),
                ],
                authenticationType: "test")),
        };
        var http = new Mock<IHttpContextAccessor>();
        http.Setup(x => x.HttpContext).Returns(httpContext);
        return http.Object;
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
