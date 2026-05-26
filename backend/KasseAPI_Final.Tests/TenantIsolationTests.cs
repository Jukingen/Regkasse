using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Tenant data isolation: API scoping, cross-tenant access, host/header resolution, Super Admin, offline queue.
/// Uses in-memory EF (no Docker). PostgreSQL integration can extend via <see cref="PostgreSqlReplayFixture"/>.
/// </summary>
public sealed class TenantIsolationTests
{
    private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private const string TenantASlug = "companyA";
    private const string TenantBSlug = "companyB";

    #region Fixtures

    private sealed class IsolationSeed
    {
        public required Guid PaymentAId { get; init; }
        public required Guid PaymentBId { get; init; }
        public required Guid CashRegisterAId { get; init; }
        public required Guid CashRegisterBId { get; init; }
    }

    private static AppDbContext CreateContext(ICurrentTenantAccessor? tenantAccessor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantIso_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, tenantAccessor ?? NullCurrentTenantAccessor.Instance);
    }

    private static async Task<(AppDbContext Db, IsolationSeed Seed)> SeedTwoTenantPaymentsAsync(
        ICurrentTenantAccessor? accessor = null)
    {
        var db = CreateContext(accessor);
        var now = DateTime.UtcNow;

        db.Tenants.AddRange(
            new Tenant
            {
                Id = TenantAId,
                Name = "Company A",
                Slug = TenantASlug,
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = now,
            },
            new Tenant
            {
                Id = TenantBId,
                Name = "Company B",
                Slug = TenantBSlug,
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = now,
            });

        var regA = Guid.NewGuid();
        var regB = Guid.NewGuid();
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        db.CashRegisters.AddRange(
            new CashRegister
            {
                TenantId = TenantAId,
                Id = regA,
                RegisterNumber = "KA-1",
                Location = "A",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Open,
                CreatedAt = now,
                IsActive = true,
            },
            new CashRegister
            {
                TenantId = TenantBId,
                Id = regB,
                RegisterNumber = "KB-1",
                Location = "B",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Open,
                CreatedAt = now,
                IsActive = true,
            });

        db.Customers.AddRange(
            new Customer { Id = customerA, Name = "CA", Email = "a@test", Phone = "1", IsActive = true },
            new Customer { Id = customerB, Name = "CB", Email = "b@test", Phone = "2", IsActive = true });

        await db.SaveChangesAsync();

        var payA = Guid.NewGuid();
        var payB = Guid.NewGuid();
        db.PaymentDetails.AddRange(
            BuildPayment(payA, customerA, regA, "AT-KA-1-20260101-1"),
            BuildPayment(payB, customerB, regB, "AT-KB-1-20260101-1"));
        await db.SaveChangesAsync();

        return (db, new IsolationSeed
        {
            PaymentAId = payA,
            PaymentBId = payB,
            CashRegisterAId = regA,
            CashRegisterBId = regB,
        });
    }

    private static PaymentDetails BuildPayment(Guid id, Guid customerId, Guid cashRegisterId, string receiptNumber) =>
        new()
        {
            Id = id,
            CustomerId = customerId,
            CustomerName = "Test",
            TableNumber = 1,
            CashierId = "cashier-1",
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            TseSignature = "eyJ.eyJ.sig",
            TseTimestamp = DateTime.UtcNow,
            ReceiptNumber = receiptNumber,
            TaxDetails = JsonDocument.Parse("{}"),
            PaymentItems = JsonDocument.Parse("[]"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

    private static ClaimsPrincipal UserWithTenant(Guid tenantId) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ScopeCheckService.TenantIdClaim, tenantId.ToString("D")),
            new Claim(ClaimTypes.NameIdentifier, "user-a"),
        }, authenticationType: "Test"));

    private static (SettingsTenantResolver Resolver, IHttpContextAccessor HttpAccessor) CreateTenantResolver(
        AppDbContext db,
        ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
        var httpAccessor = new Mock<IHttpContextAccessor>();
        httpAccessor.Setup(h => h.HttpContext).Returns(httpContext);
        var resolver = new SettingsTenantResolver(httpAccessor.Object, new AuthTenantSnapshotProvider(db));
        return (resolver, httpAccessor.Object);
    }

    private static AdminPaymentsController CreateAdminPaymentsController(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver)
    {
        return new AdminPaymentsController(
            db,
            Mock.Of<IPaymentService>(),
            Mock.Of<IReceiptPdfService>(),
            Mock.Of<ILogger<AdminPaymentsController>>(),
            tenantResolver);
    }

    private static PaymentService CreatePaymentService(AppDbContext db, ISettingsTenantResolver tenantResolver)
    {
        var paymentRepo = new GenericRepository<PaymentDetails>(db, Mock.Of<ILogger<GenericRepository<PaymentDetails>>>());
        var productRepo = new GenericRepository<Product>(db, Mock.Of<ILogger<GenericRepository<Product>>>());
        var customerRepo = new GenericRepository<Customer>(db, Mock.Of<ILogger<GenericRepository<Customer>>>());
        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Iso",
            TaxNumber = "ATU12345678",
            Street = "S",
            ZipCode = "1010",
            City = "Wien",
            FooterText = "",
        };

        return new PaymentService(
            db,
            paymentRepo,
            productRepo,
            customerRepo,
            Mock.Of<ITseService>(),
            Mock.Of<IFinanzOnlineService>(),
            Mock.Of<IUserService>(),
            new NoOpProductModifierValidationService(),
            Mock.Of<IReceiptSequenceService>(),
            Mock.Of<IReceiptService>(),
            Mock.Of<IAuditLogService>(),
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            Options.Create(new TseOptions { TseMode = "Demo" }),
            Options.Create(new InventoryOptions()),
            Mock.Of<ILogger<PaymentService>>(),
            Mock.Of<ICashRegisterResolutionService>(),
            Mock.Of<IHttpContextAccessor>(),
            new PaymentMethodCatalogService(db, tenantResolver),
            new PricingRuleResolver(db, tenantResolver),
            tenantResolver);
    }

    #endregion

    #region 1–2 API payment isolation (admin list + cross-tenant 404)

    [Fact]
    public async Task AdminPayments_List_AsTenantA_ReturnsOnlyTenantAPayments()
    {
        var (db, seed) = await SeedTwoTenantPaymentsAsync();
        await using (db)
        {
            var (resolver, _) = CreateTenantResolver(db, UserWithTenant(TenantAId));
            var controller = CreateAdminPaymentsController(db, resolver);

            var result = await controller.GetList(pageSize: 50);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<AdminPaymentsListResponse>(ok.Value);
            Assert.All(body.Items, item =>
                Assert.DoesNotContain(seed.PaymentBId, body.Items.Select(i => i.Id)));
            Assert.Contains(body.Items, i => i.Id == seed.PaymentAId);
            Assert.DoesNotContain(body.Items, i => i.Id == seed.PaymentBId);
        }
    }

    [Fact]
    public async Task AdminPayments_GetById_CrossTenant_Returns404_Not403()
    {
        var (db, seed) = await SeedTwoTenantPaymentsAsync();
        await using (db)
        {
            var (resolver, _) = CreateTenantResolver(db, UserWithTenant(TenantAId));
            var controller = CreateAdminPaymentsController(db, resolver);

            var result = await controller.GetDetail(seed.PaymentBId);

            var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        }
    }

    [Fact]
    public async Task PosPayment_GetById_CrossTenant_ReturnsNull_PreventsLeak()
    {
        var (db, seed) = await SeedTwoTenantPaymentsAsync();
        await using (db)
        {
            var (resolver, _) = CreateTenantResolver(db, UserWithTenant(TenantAId));
            var paymentService = CreatePaymentService(db, resolver);

            var payment = await paymentService.GetPaymentAsync(seed.PaymentBId);

            Assert.Null(payment);
        }
    }

    #endregion

    #region 3–4 Host subdomain and dev header resolution

    [Theory]
    [InlineData("companyA.localhost", TenantASlug)]
    [InlineData("companyB.localhost", TenantBSlug)]
    public async Task SubdomainResolution_LocalhostStyleHost_MapsToTenantSlug(string host, string expectedSlug)
    {
        await using var db = CreateContext();
        db.Tenants.AddRange(
            new Tenant { Id = TenantAId, Name = "A", Slug = TenantASlug, Status = TenantStatuses.Active, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { Id = TenantBId, Name = "B", Slug = TenantBSlug, Status = TenantStatuses.Active, IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var provider = SubdomainTenantProviderTestsHelper.Create(host, isDevelopment: false);
        Assert.Equal(expectedSlug, provider.GetCurrentTenantId());

        var accessor = new CurrentTenantAccessor();
        var service = new CurrentTenantService(
            provider,
            accessor,
            db,
            Mock.Of<ILogger<CurrentTenantService>>());
        await service.ApplyCurrentTenantAsync();

        var expectedId = expectedSlug == TenantASlug ? TenantAId : TenantBId;
        Assert.Equal(expectedId, accessor.TenantId);
    }

    [Fact]
    public async Task DevelopmentHeader_XTenantId_ResolvesToTenantGuid()
    {
        await using var db = CreateContext();
        db.Tenants.Add(new Tenant
        {
            Id = TenantAId,
            Name = "Cafe",
            Slug = "test_cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var provider = SubdomainTenantProviderTestsHelper.Create(
            "localhost",
            isDevelopment: true,
            headerTenant: "test_cafe");
        Assert.Equal("test_cafe", provider.GetCurrentTenantId());

        var accessor = new CurrentTenantAccessor();
        var service = new CurrentTenantService(
            provider,
            accessor,
            db,
            Mock.Of<ILogger<CurrentTenantService>>());
        await service.ApplyCurrentTenantAsync();

        Assert.Equal(TenantAId, accessor.TenantId);
    }

    #endregion

    #region 5 Super Admin

    [Fact]
    public async Task SuperAdmin_ListTenants_SeesAllActiveTenants()
    {
        await using var db = CreateContext();
        db.Tenants.AddRange(
            new Tenant { Id = TenantAId, Name = "A", Slug = TenantASlug, Status = TenantStatuses.Active, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { Id = TenantBId, Name = "B", Slug = TenantBSlug, Status = TenantStatuses.Active, IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new AdminTenantService(
            db,
            CreateUserManagerStub(),
            Mock.Of<ITokenClaimsService>(),
            Mock.Of<IRefreshTokenService>(),
            Mock.Of<IJwtAccessTokenIssuer>(),
            Options.Create(new AuthOptions()),
            Mock.Of<ITenantOnboardingService>(),
            new TenantService(db, Mock.Of<IAuditLogService>(), Mock.Of<ILogger<TenantService>>()),
            Mock.Of<ICashRegisterDecommissionService>(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            NullCurrentTenantAccessor.Instance,
            Mock.Of<ILogger<AdminTenantService>>());

        var list = await service.ListAsync(includeDeleted: false);

        Assert.Equal(2, list.Count);
        Assert.Contains(list, t => t.Slug == TenantASlug);
        Assert.Contains(list, t => t.Slug == TenantBSlug);
    }

    [Fact]
    public async Task SuperAdmin_WithoutImpersonation_AdminPayments_StillScopedToJwtTenant()
    {
        var (db, seed) = await SeedTwoTenantPaymentsAsync();
        await using (db)
        {
            var (resolver, _) = CreateTenantResolver(db, UserWithTenant(TenantAId));
            var controller = CreateAdminPaymentsController(db, resolver);

            var result = await controller.GetList(pageSize: 50);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<AdminPaymentsListResponse>(ok.Value);

            Assert.DoesNotContain(body.Items, i => i.Id == seed.PaymentBId);
        }
    }

    [Fact]
    public async Task SuperAdmin_ImpersonationClaim_ScopesPaymentsToTargetTenant()
    {
        var (db, seed) = await SeedTwoTenantPaymentsAsync();
        await using (db)
        {
            var (resolver, _) = CreateTenantResolver(db, UserWithTenant(TenantBId));
            var controller = CreateAdminPaymentsController(db, resolver);

            var result = await controller.GetList(pageSize: 50);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<AdminPaymentsListResponse>(ok.Value);

            Assert.Contains(body.Items, i => i.Id == seed.PaymentBId);
            Assert.DoesNotContain(body.Items, i => i.Id == seed.PaymentAId);
        }
    }

    #endregion

    #region 6 Offline queue + EF global filter

    [Fact]
    public async Task OfflineTransaction_GlobalFilter_IsolatesByAmbientTenant()
    {
        var accessorA = new CurrentTenantAccessor();
        var (db, seed) = await SeedTwoTenantPaymentsAsync(accessorA);
        await using (db)
        {
            accessorA.TenantId = TenantAId;
            db.OfflineTransactions.Add(new OfflineTransaction
            {
                Id = Guid.NewGuid(),
                CashRegisterId = seed.CashRegisterAId,
                PayloadJson = "{}",
                ServerReceivedAtUtc = DateTime.UtcNow,
                OfflineCreatedAtUtc = DateTime.UtcNow,
                Status = OfflineTransactionStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
            await db.SaveChangesAsync();

            var row = await db.OfflineTransactions.IgnoreQueryFilters().SingleAsync();
            Assert.Equal(TenantAId, row.TenantId);

            accessorA.TenantId = TenantBId;
            var visibleToB = await db.OfflineTransactions.ToListAsync();
            Assert.Empty(visibleToB);
        }
    }

    [Fact]
    public async Task OfflineTransaction_StampedOnInsert_DerivesTenantFromCashRegister()
    {
        var accessor = new CurrentTenantAccessor();
        var (db, seed) = await SeedTwoTenantPaymentsAsync(accessor);
        await using (db)
        {
            accessor.TenantId = TenantAId;
            db.OfflineTransactions.Add(new OfflineTransaction
            {
                Id = Guid.NewGuid(),
                CashRegisterId = seed.CashRegisterAId,
                PayloadJson = "{}",
                ServerReceivedAtUtc = DateTime.UtcNow,
                OfflineCreatedAtUtc = DateTime.UtcNow,
                Status = OfflineTransactionStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
            await db.SaveChangesAsync();

            var row = await db.OfflineTransactions.IgnoreQueryFilters().SingleAsync();
            Assert.Equal(TenantAId, row.TenantId);
        }
    }

    [Fact]
    public async Task Product_GlobalFilter_TenantA_CannotQueryTenantBProducts()
    {
        var accessor = new CurrentTenantAccessor { TenantId = TenantAId };
        await using var db = CreateContext(accessor);
        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        db.Tenants.AddRange(
            new Tenant { Id = TenantAId, Name = "A", Slug = TenantASlug, Status = TenantStatuses.Active, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { Id = TenantBId, Name = "B", Slug = TenantBSlug, Status = TenantStatuses.Active, IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Categories.AddRange(
            new Category { Id = catA, TenantId = TenantAId, Name = "A-cat", VatRate = 10m },
            new Category { Id = catB, TenantId = TenantBId, Name = "B-cat", VatRate = 10m });
        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = TenantBId,
            Name = "Secret B",
            Price = 1m,
            CategoryId = catB,
            Category = "B-cat",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            TaxRate = 10m,
            Barcode = "b-only",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var visible = await db.Products.Select(p => p.Name).ToListAsync();
        Assert.Empty(visible);
    }

    #endregion

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
}

/// <summary>Shared helper for subdomain provider construction in isolation tests.</summary>
internal static class SubdomainTenantProviderTestsHelper
{
    public static SubdomainTenantProvider Create(
        string host,
        bool isDevelopment,
        string? headerTenant = null,
        string? queryTenant = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);
        if (!string.IsNullOrEmpty(headerTenant))
            httpContext.Request.Headers[SubdomainTenantProvider.DevTenantHeaderName] = headerTenant;
        if (!string.IsNullOrEmpty(queryTenant))
            httpContext.Request.QueryString = new QueryString($"?{SubdomainTenantProvider.DevTenantQueryName}={queryTenant}");

        var httpAccessor = new Mock<IHttpContextAccessor>();
        httpAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.EnvironmentName)
            .Returns(isDevelopment ? Environments.Development : Environments.Production);

        return new SubdomainTenantProvider(httpAccessor.Object, environment.Object);
    }
}
