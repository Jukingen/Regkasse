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
using TenantLicenseService = KasseAPI_Final.Services.Billing.TenantLicenseService;

namespace KasseAPI_Final.Tests;

public sealed class TenantLicenseServiceTests
{
    [Fact]
    public async Task GetCurrentStatus_NoLicense_ReturnsNone()
    {
        var service = CreateService();
        var tenant = await CreateTestTenant();

        var status = await service.GetCurrentStatusAsync(tenant.Id);

        Assert.Equal("none", status.Status);
        Assert.False(status.IsValid);
        Assert.Null(status.LicenseKey);
    }

    [Fact]
    public async Task ActivateLicense_ValidKey_ActivatesSuccessfully()
    {
        var service = CreateService();
        var tenant = await CreateTestTenant();
        var sale = await CreateTestLicenseSale(tenant.Id);

        var result = await service.ActivateLicenseAsync(
            tenant.Id, sale.LicenseKey, Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Equal(sale.LicenseKey, result.LicenseKey);
        Assert.Equal(sale.ValidUntilUtc, result.ValidUntilUtc);

        var updatedTenant = await GetTenant(tenant.Id);
        Assert.Equal(sale.LicenseKey, updatedTenant.LicenseKey);
    }

    [Fact]
    public async Task ActivateLicense_ExpiredKey_ReturnsError()
    {
        var service = CreateService();
        var tenant = await CreateTestTenant();
        var sale = await CreateTestLicenseSale(tenant.Id, validUntil: DateTime.UtcNow.AddDays(-1));

        var result = await service.ActivateLicenseAsync(
            tenant.Id, sale.LicenseKey, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains("abgelaufen", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetExpiringLicenses_ReturnsCorrectLicenses()
    {
        var service = CreateService();
        var tenant1 = await CreateTestTenant("soon");
        var tenant2 = await CreateTestTenant("later");

        await CreateTestLicenseSale(tenant1.Id, validUntil: DateTime.UtcNow.AddDays(15));
        await CreateTestLicenseSale(tenant2.Id, validUntil: DateTime.UtcNow.AddDays(45));

        var results = await service.GetExpiringLicensesAsync(30);

        Assert.Single(results);
        Assert.Equal(tenant1.Id, results.First().TenantId);
        Assert.True(results.First().DaysRemaining <= 30);
    }

    private TenantLicenseService CreateService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantLicenseService_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
        _factory = TenantTestDoubles.DbContextFactoryForTests(options, NullCurrentTenantAccessor.Instance);
        _db.Database.EnsureCreated();

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());

        BillingService? billingService = null;
        var pdfGenerator = CreateInvoicePdfGenerator(_factory, environment.Object, () => billingService!);
        billingService = new BillingService(
            _factory,
            new LicenseKeyGenerator(),
            BillingTestDoubles.CreateAuditService(_factory),
            BillingTestDoubles.CreateReminderScopeFactory(),
            environment.Object,
            pdfGenerator,
            BillingTestDoubles.DisabledBackupOptions,
            NullLogger<BillingService>.Instance);

        return new TenantLicenseService(
            _factory,
            billingService,
            new LicenseKeyGenerator(),
            BillingTestDoubles.CreateAuditService(_factory),
            NullLogger<TenantLicenseService>.Instance);
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

    private AppDbContext _db = null!;
    private IDbContextFactory<AppDbContext> _factory = null!;

    private async Task<Tenant> CreateTestTenant(string slug = "cafe")
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = $"Test {slug}",
            Slug = slug,
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync().ConfigureAwait(false);
        _db.ChangeTracker.Clear();
        return tenant;
    }

    private async Task<LicenseSale> CreateTestLicenseSale(
        Guid tenantId,
        DateTime? validUntil = null)
    {
        var tenant = await _db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId).ConfigureAwait(false);
        var until = validUntil ?? DateTime.UtcNow.AddDays(365);
        var actorUserId = Guid.NewGuid();

        var sale = new LicenseSale
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
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
            InvoiceNumber = $"RE{Guid.NewGuid():N}"[..12],
            Status = LicenseSaleStatuses.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.LicenseSales.Add(sale);
        await _db.SaveChangesAsync().ConfigureAwait(false);
        _db.ChangeTracker.Clear();
        return sale;
    }

    private async Task<Tenant> GetTenant(Guid tenantId) =>
        await _db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId).ConfigureAwait(false);
}
