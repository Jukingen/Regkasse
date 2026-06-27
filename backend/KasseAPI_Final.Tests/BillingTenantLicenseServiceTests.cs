using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using BillingTenantLicenseService = KasseAPI_Final.Services.Billing.TenantLicenseService;

namespace KasseAPI_Final.Tests;

public sealed class BillingTenantLicenseServiceTests
{
    [Fact]
    public async Task GetCurrentStatusAsync_ActiveLicense_ReturnsValidStatus()
    {
        await using var ctx = await CreateContextAsync();
        var tenant = await SeedTenantAsync(
            ctx.Db,
            validUntil: DateTime.UtcNow.AddDays(30),
            licenseKey: "REGK-20270101-cafe-A7F3K2D9");

        var service = CreateService(ctx);
        var status = await service.GetCurrentStatusAsync(tenant.Id);

        Assert.Equal("valid", status.Status);
        Assert.True(status.IsValid);
        Assert.True(status.IsTrial);
        Assert.InRange(status.DaysRemaining!.Value, 29, 30);
        Assert.True(status.IsExpiringSoon);
    }

    [Fact]
    public async Task IsLicenseValidAsync_WithoutLicenseKey_ReturnsFalse()
    {
        await using var ctx = await CreateContextAsync();
        var tenant = await SeedTenantAsync(ctx.Db, validUntil: DateTime.UtcNow.AddDays(10), licenseKey: null);

        var service = CreateService(ctx);
        Assert.False(await service.IsLicenseValidAsync(tenant.Id));
    }

    [Fact]
    public async Task ActivateLicenseAsync_ValidBillingKey_UpdatesTenantAndSale()
    {
        await using var ctx = await CreateContextAsync();
        var tenant = await SeedTenantAsync(ctx.Db, slug: "cafe");
        var actorUserId = await SeedUserAsync(ctx.Db);
        var sale = await CreateDetachedSaleAsync(ctx, tenant, actorUserId);

        var service = CreateService(ctx);
        var result = await service.ActivateLicenseAsync(tenant.Id, sale.LicenseKey, actorUserId);

        Assert.True(result.Success);
        Assert.Equal(sale.ValidUntilUtc, result.ValidUntilUtc);
        Assert.Equal("Lizenz wurde erfolgreich aktiviert.", result.Message);

        var updatedTenant = await ctx.Db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenant.Id);
        Assert.Equal(sale.LicenseKey, updatedTenant.LicenseKey);
        Assert.Equal(sale.Id, updatedTenant.CurrentLicenseSaleId);
        Assert.Equal(1, updatedTenant.LicenseActivationCount);

        var updatedSale = await ctx.Db.LicenseSales.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(s => s.Id == sale.Id);
        Assert.NotNull(updatedSale.ActivationDateUtc);

        var audit = await ctx.Db.BillingAuditLogs.AsNoTracking()
            .SingleAsync(a => a.SaleId == sale.Id && a.Action == BillingAuditEventTypes.LicenseActivated);
        Assert.Equal(actorUserId, audit.UserId);
    }

    [Fact]
    public async Task ActivateLicenseAsync_WrongTenant_ReturnsFailure()
    {
        await using var ctx = await CreateContextAsync();
        var tenantA = await SeedTenantAsync(ctx.Db, slug: "cafe-a");
        var tenantB = await SeedTenantAsync(ctx.Db, slug: "cafe-b");
        var actorUserId = await SeedUserAsync(ctx.Db);
        var sale = await CreateDetachedSaleAsync(ctx, tenantA, actorUserId);

        var service = CreateService(ctx);
        var result = await service.ActivateLicenseAsync(tenantB.Id, sale.LicenseKey, actorUserId);

        Assert.False(result.Success);
        Assert.Contains("anderen Mandanten", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtendLicenseAsync_ValidKey_RecordsExtensionMetadata()
    {
        await using var ctx = await CreateContextAsync();
        var tenant = await SeedTenantAsync(
            ctx.Db,
            slug: "cafe",
            validUntil: DateTime.UtcNow.AddDays(5),
            licenseKey: "REGK-OLD");
        var actorUserId = await SeedUserAsync(ctx.Db);
        var sale = await CreateDetachedSaleAsync(ctx, tenant, actorUserId);

        var service = CreateService(ctx);
        var result = await service.ExtendLicenseAsync(tenant.Id, sale.LicenseKey, actorUserId);

        Assert.True(result.Success);
        Assert.Equal(sale.ValidUntilUtc, result.ValidUntilUtc);
        Assert.Equal(LicenseSalePlans.TwelveMonths, result.LicensePlan);
        Assert.Equal("Lizenz wurde erfolgreich verlängert.", result.Message);

        var updatedSale = await ctx.Db.LicenseSales.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(s => s.Id == sale.Id);
        Assert.NotNull(updatedSale.LastExtendedAtUtc);
        Assert.Equal(actorUserId, updatedSale.ExtendedByUserId);

        var extensionAudit = await ctx.Db.BillingAuditLogs.AsNoTracking()
            .SingleAsync(a => a.SaleId == sale.Id && a.Action == BillingAuditEventTypes.LicenseExtended);
        Assert.Equal(actorUserId, extensionAudit.UserId);
    }

    [Fact]
    public async Task GetExpiringLicensesAsync_ReturnsActiveSalesWithinThreshold()
    {
        await using var ctx = await CreateContextAsync();
        var soonTenant = await SeedTenantAsync(ctx.Db, slug: "soon");
        var laterTenant = await SeedTenantAsync(ctx.Db, slug: "later");
        var actorUserId = await SeedUserAsync(ctx.Db);

        await CreateDetachedSaleAsync(
            ctx,
            soonTenant,
            actorUserId,
            validUntil: DateTime.UtcNow.AddDays(7),
            invoiceSuffix: "001");
        await CreateDetachedSaleAsync(
            ctx,
            laterTenant,
            actorUserId,
            validUntil: DateTime.UtcNow.AddDays(60),
            invoiceSuffix: "002");

        var service = CreateService(ctx);
        var expiring = await service.GetExpiringLicensesAsync(daysThreshold: 30);

        Assert.Single(expiring);
        Assert.Equal("soon", expiring[0].TenantSlug);
    }

    [Fact]
    public async Task GetLicenseHistoryAsync_ReturnsTenantSales()
    {
        await using var ctx = await CreateContextAsync();
        var tenant = await SeedTenantAsync(ctx.Db, slug: "history");
        var actorUserId = await SeedUserAsync(ctx.Db);
        await CreateDetachedSaleAsync(ctx, tenant, actorUserId, invoiceSuffix: "001");
        await CreateDetachedSaleAsync(ctx, tenant, actorUserId, invoiceSuffix: "002");

        var service = CreateService(ctx);
        var history = await service.GetLicenseHistoryAsync(tenant.Id);

        Assert.Equal(2, history.Count);
        Assert.All(history, item => Assert.Equal(tenant.Id, item.TenantId));
    }

    private static BillingTenantLicenseService CreateService(TestContext ctx)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());

        BillingService? billingService = null;
        var pdfGenerator = CreateInvoicePdfGenerator(ctx.Factory, environment.Object, () => billingService!);
        billingService = new BillingService(
            ctx.Factory,
            new LicenseKeyGenerator(),
            BillingTestDoubles.CreateAuditService(ctx.Factory),
            BillingTestDoubles.CreateReminderScopeFactory(),
            environment.Object,
            pdfGenerator,
            BillingTestDoubles.DisabledBackupOptions,
            NullLogger<BillingService>.Instance);

        return new BillingTenantLicenseService(
            ctx.Factory,
            billingService,
            new LicenseKeyGenerator(),
            BillingTestDoubles.CreateAuditService(ctx.Factory),
            NullLogger<BillingTenantLicenseService>.Instance);
    }

    private static async Task<TestContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BillingTenantLicense_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
        var factory = TenantTestDoubles.DbContextFactoryForTests(options, NullCurrentTenantAccessor.Instance);
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
        return new TestContext(db, factory);
    }

    private static async Task<Tenant> SeedTenantAsync(
        AppDbContext db,
        string slug = "cafe",
        DateTime? validUntil = null,
        string? licenseKey = null,
        Guid? currentLicenseSaleId = null)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = $"Test {slug}",
            Slug = slug,
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = validUntil,
            LicenseKey = licenseKey,
            CurrentLicenseSaleId = currentLicenseSaleId,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return tenant;
    }

    private static async Task<Guid> SeedUserAsync(AppDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = userId.ToString("D"),
            UserName = "license.manager",
            NormalizedUserName = "LICENSE.MANAGER",
            Email = "license.manager@regkasse.test",
            NormalizedEmail = "LICENSE.MANAGER@REGKASSE.TEST",
            FirstName = "License",
            LastName = "Manager",
            EmailConfirmed = true,
        });
        await db.SaveChangesAsync().ConfigureAwait(false);
        return userId;
    }

    private static async Task<LicenseSale> CreateDetachedSaleAsync(
        TestContext ctx,
        Tenant tenant,
        Guid actorUserId,
        DateTime? validUntil = null,
        string invoiceSuffix = "001")
    {
        var until = validUntil ?? DateTime.UtcNow.AddDays(365);
        var sale = new LicenseSale
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            LicenseKey = new LicenseKeyGenerator().GenerateLicenseKey(tenant.Slug, until),
            LicensePlan = LicenseSalePlans.TwelveMonths,
            ValidFromUtc = DateTime.UtcNow,
            ValidUntilUtc = until,
            PriceNet = 299.00m,
            VatRate = 20.00m,
            VatAmount = 59.80m,
            PriceGross = 358.80m,
            Currency = "EUR",
            SoldAtUtc = DateTime.UtcNow,
            SoldByUserId = actorUserId,
            InvoiceNumber = $"RE202606{invoiceSuffix}",
            Status = LicenseSaleStatuses.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        ctx.Db.LicenseSales.Add(sale);
        await ctx.Db.SaveChangesAsync().ConfigureAwait(false);
        ctx.Db.ChangeTracker.Clear();
        return sale;
    }

    private sealed class TestContext : IAsyncDisposable
    {
        public TestContext(AppDbContext db, IDbContextFactory<AppDbContext> factory)
        {
            Db = db;
            Factory = factory;
        }

        public AppDbContext Db { get; }
        public IDbContextFactory<AppDbContext> Factory { get; }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private static InvoicePdfGenerator CreateInvoicePdfGenerator(
        IDbContextFactory<AppDbContext> factory,
        IWebHostEnvironment environment,
        Func<IBillingService> billingServiceFactory)
    {
        var configuration = new ConfigurationBuilder().Build();
        var templateService = new InvoicePdfTemplateService(configuration, environment);

        var services = new ServiceCollection();
        services.AddSingleton(billingServiceFactory);
        services.AddScoped<IBillingService>(sp => sp.GetRequiredService<Func<IBillingService>>()());
        var provider = services.BuildServiceProvider();

        return new InvoicePdfGenerator(
            factory,
            provider.GetRequiredService<IServiceScopeFactory>(),
            templateService,
            NullLogger<InvoicePdfGenerator>.Instance,
            configuration);
    }
}
