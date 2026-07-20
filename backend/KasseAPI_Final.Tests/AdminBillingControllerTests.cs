using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.DigitalServices;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using IBillingTenantLicenseService = KasseAPI_Final.Services.Billing.ITenantLicenseService;

namespace KasseAPI_Final.Tests;

public sealed class AdminBillingControllerTests
{
    [Fact]
    public async Task CreateLicenseSale_ValidRequest_ReturnsCreated()
    {
        var ctx = await AdminBillingTestContext.CreateAsync();
        await using var _ = ctx;

        var controller = ctx.CreateController();
        var tenant = await ctx.CreateTestTenantAsync();
        var request = new CreateLicenseSaleRequest
        {
            TenantId = tenant.Id,
            LicensePlan = LicenseSalePlans.TwelveMonths,
            PriceNet = 299.00m,
        };

        var result = await controller.CreateLicenseSale(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<LicenseSaleResponse>(createdResult.Value);
        Assert.Equal(299.00m, response.PriceNet);
        Assert.Equal(LicenseSaleStatuses.Active, response.Status);
        Assert.StartsWith("RE", response.InvoiceNumber, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListLicenseSales_WithFilters_ReturnsFilteredList()
    {
        var ctx = await AdminBillingTestContext.CreateAsync();
        await using var _ = ctx;

        var controller = ctx.CreateController();
        var tenant1 = await ctx.CreateTestTenantAsync("tenant-one");
        var tenant2 = await ctx.CreateTestTenantAsync("tenant-two");
        await ctx.CreateTestSaleAsync(tenant1.Id);
        await ctx.CreateTestSaleAsync(tenant2.Id);

        var result = await controller.ListLicenseSales(
            page: 1,
            pageSize: 10,
            tenantId: tenant1.Id,
            ct: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LicenseSaleListResponse>(okResult.Value);
        Assert.Single(response.Items);
        Assert.Equal(tenant1.Id, response.Items[0].TenantId);
    }

    [Fact]
    public async Task DownloadPdf_ExistingSale_ReturnsPdfFile()
    {
        var ctx = await AdminBillingTestContext.CreateAsync();
        await using var _ = ctx;

        var sale = await ctx.CreateTestSaleAsync();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

        var controller = ctx.CreateController();
        ctx.PdfGeneratorMock
            .Setup(x => x.GenerateInvoicePdfAsync(sale.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfBytes);

        var result = await controller.DownloadPdf(sale.Id, CancellationToken.None);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.StartsWith($"RE-{sale.InvoiceNumber}-", fileResult.FileDownloadName, StringComparison.Ordinal);
        Assert.Equal(pdfBytes, fileResult.FileContents);
    }

    [Fact]
    public async Task CreateLicenseSale_WithoutActorUser_ReturnsUnauthorized()
    {
        var ctx = await AdminBillingTestContext.CreateAsync();
        await using var _ = ctx;

        var controller = ctx.CreateController(includeActorUser: false);
        var tenant = await ctx.CreateTestTenantAsync();
        var request = new CreateLicenseSaleRequest
        {
            TenantId = tenant.Id,
            LicensePlan = LicenseSalePlans.TwelveMonths,
            PriceNet = 299.00m,
        };

        var result = await controller.CreateLicenseSale(request, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetLicenseSale_UnknownId_ReturnsNotFound()
    {
        var ctx = await AdminBillingTestContext.CreateAsync();
        await using var _ = ctx;

        var controller = ctx.CreateController();
        var result = await controller.GetLicenseSale(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private sealed class AdminBillingTestContext : IAsyncDisposable
    {
        private readonly AppDbContext _db;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private Guid? _actorUserId;

        public Mock<IInvoicePdfGenerator> PdfGeneratorMock { get; } = new();

        private AdminBillingTestContext(AppDbContext db, IDbContextFactory<AppDbContext> factory)
        {
            _db = db;
            _factory = factory;
        }

        public static Task<AdminBillingTestContext> CreateAsync()
        {
            var dbName = $"AdminBillingController_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
            var factory = TenantTestDoubles.DbContextFactoryForTests(options, NullCurrentTenantAccessor.Instance);
            return Task.FromResult(new AdminBillingTestContext(db, factory));
        }

        public AdminBillingController CreateController(bool includeActorUser = true)
        {
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());

            PdfGeneratorMock
                .Setup(x => x.GenerateInvoicePdfAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });

            var billingService = new BillingService(
                _db,
                new LicenseKeyGenerator(),
                BillingTestDoubles.CreateAuditService(_db),
                BillingTestDoubles.CreateReminderScopeFactory(),
                environment.Object,
                PdfGeneratorMock.Object,
                BillingTestDoubles.DisabledBackupOptions,
                NullLogger<BillingService>.Instance);

            var tenantLicenseService = new TenantLicenseService(
                _db,
                billingService,
                new LicenseKeyGenerator(),
                BillingTestDoubles.CreateAuditService(_db),
                NullLogger<TenantLicenseService>.Instance);

            var controller = new AdminBillingController(
                billingService,
                tenantLicenseService,
                PdfGeneratorMock.Object,
                BillingTestDoubles.CreateAuditService(_db),
                BillingTestDoubles.NoOpReminder,
                BillingTestDoubles.NoOpLicenseReminder,
                BillingTestDoubles.NoOpBackup,
                new DigitalServicePricingService(),
                Mock.Of<ISubscriptionService>(),
                NullCurrentUserService.Instance,
                NullLogger<AdminBillingController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };

            if (includeActorUser)
            {
                var actorUserId = EnsureActorUserId();
                controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, actorUserId.ToString("D")),
                        new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                    ],
                    authenticationType: "Test"));
            }

            return controller;
        }

        private Guid EnsureActorUserId()
        {
            if (_actorUserId.HasValue)
                return _actorUserId.Value;

            _actorUserId = Guid.NewGuid();
            _db.Users.Add(new ApplicationUser
            {
                Id = _actorUserId.Value.ToString("D"),
                UserName = "billing.superadmin",
                NormalizedUserName = "BILLING.SUPERADMIN",
                Email = "billing.superadmin@regkasse.test",
                NormalizedEmail = "BILLING.SUPERADMIN@REGKASSE.TEST",
                FirstName = "Billing",
                LastName = "SuperAdmin",
                EmailConfirmed = true,
            });
            _db.SaveChanges();
            return _actorUserId.Value;
        }

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

        public async Task<LicenseSaleResponse> CreateTestSaleAsync(
            Guid? tenantId = null,
            decimal priceNet = 299.00m)
        {
            var tenant = tenantId.HasValue
                ? await _db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId.Value).ConfigureAwait(false)
                : await CreateTestTenantAsync().ConfigureAwait(false);

            _actorUserId ??= EnsureActorUserId();
            if (!await _db.Users.AnyAsync(u => u.Id == _actorUserId.Value.ToString("D")).ConfigureAwait(false))
            {
                _db.Users.Add(new ApplicationUser
                {
                    Id = _actorUserId.Value.ToString("D"),
                    UserName = "billing.superadmin",
                    NormalizedUserName = "BILLING.SUPERADMIN",
                    Email = "billing.superadmin@regkasse.test",
                    NormalizedEmail = "BILLING.SUPERADMIN@REGKASSE.TEST",
                    FirstName = "Billing",
                    LastName = "SuperAdmin",
                    EmailConfirmed = true,
                });
                await _db.SaveChangesAsync().ConfigureAwait(false);
            }

            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());

            var billingService = new BillingService(
                _db,
                new LicenseKeyGenerator(),
                BillingTestDoubles.CreateAuditService(_db),
                BillingTestDoubles.CreateReminderScopeFactory(),
                environment.Object,
                PdfGeneratorMock.Object,
                BillingTestDoubles.DisabledBackupOptions,
                NullLogger<BillingService>.Instance);

            return await billingService.CreateLicenseSaleAsync(
                new CreateLicenseSaleRequest
                {
                    TenantId = tenant.Id,
                    LicensePlan = LicenseSalePlans.TwelveMonths,
                    PriceNet = priceNet,
                },
                _actorUserId.Value).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync() => _db.DisposeAsync();
    }
}
