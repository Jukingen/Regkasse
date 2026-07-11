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

namespace KasseAPI_Final.Tests.Billing;

/// <summary>Shared in-memory DB + factory for billing service unit tests.</summary>
internal sealed class BillingServiceTestHarness : IAsyncDisposable
{
    private readonly AppDbContext _db;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private Guid? _defaultActorUserId;

    private BillingServiceTestHarness(AppDbContext db, IDbContextFactory<AppDbContext> factory)
    {
        _db = db;
        _factory = factory;
    }

    public static Task<BillingServiceTestHarness> CreateAsync()
    {
        var (db, factory) = BillingServiceTestInfrastructure.CreateDb();
        return Task.FromResult(new BillingServiceTestHarness(db, factory));
    }

    public BillingService CreateBillingService(string? contentRootPath = null) =>
        BillingServiceTestInfrastructure.CreateService(_db, contentRootPath);

    public async Task<Tenant> CreateTestTenantAsync(string slug = "dev")
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
        return tenant;
    }

    public async Task<Guid> CreateTestUserAsync()
    {
        if (_defaultActorUserId.HasValue)
            return _defaultActorUserId.Value;

        var userId = Guid.NewGuid();
        _db.Users.Add(new ApplicationUser
        {
            Id = userId.ToString("D"),
            UserName = "billing.tester",
            NormalizedUserName = "BILLING.TESTER",
            Email = "billing.tester@regkasse.test",
            NormalizedEmail = "BILLING.TESTER@REGKASSE.TEST",
            FirstName = "Billing",
            LastName = "Tester",
            EmailConfirmed = true,
        });
        await _db.SaveChangesAsync().ConfigureAwait(false);
        _defaultActorUserId = userId;
        return userId;
    }

    public async Task<LicenseSaleResponse> CreateTestSaleAsync(
        Guid? tenantId = null,
        decimal priceNet = 299.00m,
        string licensePlan = LicenseSalePlans.TwelveMonths)
    {
        var tenant = tenantId.HasValue
            ? await GetTenantAsync(tenantId.Value).ConfigureAwait(false)
            : await CreateTestTenantAsync().ConfigureAwait(false);
        var actorUserId = await CreateTestUserAsync().ConfigureAwait(false);

        return await CreateBillingService().CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = licensePlan,
                PriceNet = priceNet,
            },
            actorUserId).ConfigureAwait(false);
    }

    public async Task<Tenant> GetTenantAsync(Guid tenantId)
    {
        _db.ChangeTracker.Clear();
        return await _db.Tenants.AsNoTracking()
            .SingleAsync(t => t.Id == tenantId)
            .ConfigureAwait(false);
    }

    public (AppDbContext Db, IDbContextFactory<AppDbContext> Factory) CreateDbContextPair() => (_db, _factory);

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}

internal static class BillingServiceTestInfrastructure
{
    internal static BillingService CreateService(
        AppDbContext db,
        string? contentRootPath = null)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(contentRootPath ?? Path.GetTempPath());

        BillingService? billingService = null;
        var pdfGenerator = CreateInvoicePdfGenerator(db, environment.Object, () => billingService!);
        billingService = new BillingService(
            db,
            new LicenseKeyGenerator(),
            BillingTestDoubles.CreateAuditService(db),
            BillingTestDoubles.CreateReminderScopeFactory(),
            environment.Object,
            pdfGenerator,
            BillingTestDoubles.DisabledBackupOptions,
            NullLogger<BillingService>.Instance);

        return billingService;
    }

    internal static InvoicePdfGenerator CreateInvoicePdfGenerator(
        AppDbContext db,
        IWebHostEnvironment environment,
        Func<IBillingService> billingServiceFactory)
    {
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddSingleton(billingServiceFactory);
        services.AddScoped<IBillingService>(sp => sp.GetRequiredService<Func<IBillingService>>()());
        var provider = services.BuildServiceProvider();

        return new InvoicePdfGenerator(
            provider.GetRequiredService<IServiceScopeFactory>(),
            configuration,
            NullLogger<InvoicePdfGenerator>.Instance);
    }

    internal static (AppDbContext Db, IDbContextFactory<AppDbContext> Factory) CreateDb()
    {
        var dbName = $"BillingService_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
        var factory = TenantTestDoubles.DbContextFactoryForTests(options, NullCurrentTenantAccessor.Instance);
        return (db, factory);
    }

    internal static Tenant SeedTenant(AppDbContext db, string slug, string status = TenantStatuses.Active)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Cafe Demo",
            Slug = slug,
            Status = status,
            IsActive = status == TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        db.SaveChanges();
        return tenant;
    }

    internal static string SeedUser(AppDbContext db)
    {
        var userId = Guid.NewGuid().ToString("D");
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "superadmin",
            NormalizedUserName = "SUPERADMIN",
            Email = "superadmin@regkasse.test",
            NormalizedEmail = "SUPERADMIN@REGKASSE.TEST",
            FirstName = "Super",
            LastName = "Admin",
            EmailConfirmed = true,
        });
        db.SaveChanges();
        return userId;
    }
}
