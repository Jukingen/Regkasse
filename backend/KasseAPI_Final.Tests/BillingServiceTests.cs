using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BillingServiceTests
{
    // --- Preview license sale ---

    [Fact]
    public async Task PreviewLicenseSaleAsync_ComputesTwelveMonthPricing()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var service = CreateService(db);

        var preview = await service.PreviewLicenseSaleAsync(
            new LicenseSalePreviewRequest(tenant.Id, LicenseSalePlans.TwelveMonths, null, 100m, 20m));

        Assert.Equal("cafe", preview.TenantSlug);
        Assert.Equal(20m, preview.VatAmount);
        Assert.Equal(120m, preview.PriceGross);
        Assert.StartsWith("REGK-", preview.LicenseKey, StringComparison.Ordinal);
        Assert.StartsWith("RE", preview.InvoiceNumber, StringComparison.Ordinal);
        Assert.True(new LicenseKeyGenerator().ValidateLicenseKeyFormat(preview.LicenseKey));
    }

    [Fact]
    public async Task PreviewLicenseSaleAsync_CustomPlan_UsesProvidedValidUntil()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var service = CreateService(db);
        var customUntil = DateTime.UtcNow.AddDays(45);

        var preview = await service.PreviewLicenseSaleAsync(
            new LicenseSalePreviewRequest(tenant.Id, LicenseSalePlans.Custom, customUntil, 75m));

        Assert.Equal(customUntil.ToUniversalTime().Date, preview.ValidUntilUtc.ToUniversalTime().Date);
        Assert.Equal(15m, preview.VatAmount);
        Assert.Equal(90m, preview.PriceGross);
    }

    [Fact]
    public async Task PreviewLicenseSaleAsync_TenantNotFound_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.PreviewLicenseSaleAsync(
                new LicenseSalePreviewRequest(Guid.NewGuid(), LicenseSalePlans.SixMonths, null, 50m)));
    }

    [Theory]
    [InlineData("invalid_plan")]
    [InlineData("")]
    public async Task PreviewLicenseSaleAsync_InvalidPlan_Throws(string plan)
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.PreviewLicenseSaleAsync(
                new LicenseSalePreviewRequest(tenant.Id, plan, null, 50m)));

        Assert.Contains("license plan", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewLicenseSaleAsync_CustomPlanWithoutDate_Throws()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.PreviewLicenseSaleAsync(
                new LicenseSalePreviewRequest(tenant.Id, LicenseSalePlans.Custom, null, 50m)));

        Assert.Contains("CustomValidUntilUtc", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewLicenseSaleAsync_NonPositivePrice_Throws()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.PreviewLicenseSaleAsync(
                new LicenseSalePreviewRequest(tenant.Id, LicenseSalePlans.SixMonths, null, 0m)));

        Assert.Contains("PriceNet", ex.Message, StringComparison.Ordinal);
    }

    // --- Create license sale ---

    [Fact]
    public async Task CreateLicenseSaleAsync_UpdatesTenantLicenseAndPersistsSale()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(db);

        var response = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest(tenant.Id, LicenseSalePlans.SixMonths, null, 50m, 20m, "Initial sale"),
            Guid.Parse(userId));

        Assert.Equal(LicenseSaleStatuses.Active, response.Status);
        Assert.Equal("Initial sale", response.Notes);
        Assert.Equal(10m, response.VatAmount);
        Assert.Equal(60m, response.PriceGross);
        Assert.True(new LicenseKeyGenerator().ValidateLicenseKeyFormat(response.LicenseKey));

        var updatedTenant = await db.Tenants.SingleAsync(t => t.Id == tenant.Id);
        Assert.Equal(response.LicenseKey, updatedTenant.LicenseKey);
        Assert.NotNull(updatedTenant.LicenseValidUntilUtc);
        Assert.Equal(1, await db.LicenseSales.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await db.BillingAuditLogs.CountAsync());
        var auditRow = await db.BillingAuditLogs.SingleAsync();
        Assert.Equal(BillingAuditEventTypes.SaleCreated, auditRow.Action);
        Assert.Contains("\"priceGross\":60", auditRow.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateLicenseSaleAsync_DeletedTenant_Throws()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe", TenantStatuses.Deleted);
        var userId = SeedUser(db);
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateLicenseSaleAsync(
                new CreateLicenseSaleRequest(tenant.Id, LicenseSalePlans.SixMonths, null, 50m),
                Guid.Parse(userId)));
    }

    [Fact]
    public async Task CreateLicenseSaleAsync_UnknownUser_Throws()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateLicenseSaleAsync(
                new CreateLicenseSaleRequest(tenant.Id, LicenseSalePlans.SixMonths, null, 50m),
                Guid.NewGuid()));

        Assert.Contains("User not found", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateLicenseSaleAsync_CustomPlanBeforeStart_Throws()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateLicenseSaleAsync(
                new CreateLicenseSaleRequest(
                    tenant.Id,
                    LicenseSalePlans.Custom,
                    DateTime.UtcNow.AddDays(-1),
                    50m),
                Guid.Parse(userId)));

        Assert.Contains("CustomValidUntilUtc", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateLicenseSaleAsync_NegativeVatRate_Throws()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateLicenseSaleAsync(
                new CreateLicenseSaleRequest(tenant.Id, LicenseSalePlans.SixMonths, null, 50m, -1m),
                Guid.Parse(userId)));

        Assert.Contains("VatRate", ex.Message, StringComparison.Ordinal);
    }

    // --- List with filters ---

    [Fact]
    public async Task ListLicenseSalesAsync_FiltersByTenantStatusAndSearch()
    {
        await using var db = CreateDb();
        var cafe = SeedTenant(db, "cafe");
        var bar = SeedTenant(db, "bar");
        var userId = SeedUser(db);
        var service = CreateService(db);

        var cafeSale = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest(cafe.Id, LicenseSalePlans.SixMonths, null, 50m, 20m, "Cafe note"),
            Guid.Parse(userId));
        var barSale = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest(bar.Id, LicenseSalePlans.TwelveMonths, null, 100m),
            Guid.Parse(userId));

        await service.CancelLicenseSaleAsync(
            barSale.Id,
            new CancelLicenseSaleRequest("Test cancel"),
            Guid.Parse(userId));

        var tenantFilter = await service.ListLicenseSalesAsync(
            new LicenseSaleListQuery(TenantId: cafe.Id.ToString()));
        Assert.Single(tenantFilter.Items);
        Assert.Equal(cafeSale.Id, tenantFilter.Items[0].Id);

        var activeOnly = await service.ListLicenseSalesAsync(
            new LicenseSaleListQuery(Status: LicenseSaleStatuses.Active));
        Assert.Single(activeOnly.Items);
        Assert.Equal(cafeSale.Id, activeOnly.Items[0].Id);

        var searchBySlug = await service.ListLicenseSalesAsync(
            new LicenseSaleListQuery(Search: "cafe"));
        Assert.Single(searchBySlug.Items);
        Assert.Equal(cafeSale.LicenseKey, searchBySlug.Items[0].LicenseKey);

        var searchByInvoice = await service.ListLicenseSalesAsync(
            new LicenseSaleListQuery(Search: cafeSale.InvoiceNumber));
        Assert.Single(searchByInvoice.Items);
    }

    [Fact]
    public async Task ListLicenseSalesAsync_FiltersBySoldDateRange()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(db);

        var sale = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest(tenant.Id, LicenseSalePlans.SixMonths, null, 50m),
            Guid.Parse(userId));

        var row = await db.LicenseSales.IgnoreQueryFilters().SingleAsync(s => s.Id == sale.Id);
        row.SoldAtUtc = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var inside = await service.ListLicenseSalesAsync(new LicenseSaleListQuery(
            FromDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ToDate: new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc)));
        Assert.Single(inside.Items);

        var outside = await service.ListLicenseSalesAsync(new LicenseSaleListQuery(
            FromDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Empty(outside.Items);
    }

    [Fact]
    public async Task ListLicenseSalesAsync_PaginatesResults()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(db);

        for (var i = 0; i < 3; i++)
        {
            await service.CreateLicenseSaleAsync(
                new CreateLicenseSaleRequest(tenant.Id, LicenseSalePlans.SixMonths, null, 10m + i),
                Guid.Parse(userId));
        }

        var page1 = await service.ListLicenseSalesAsync(new LicenseSaleListQuery(Page: 1, PageSize: 2));
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);

        var page2 = await service.ListLicenseSalesAsync(new LicenseSaleListQuery(Page: 2, PageSize: 2));
        Assert.Single(page2.Items);
    }

    [Fact]
    public async Task ListLicenseSalesAsync_InvalidStatusFilter_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ListLicenseSalesAsync(new LicenseSaleListQuery(Status: "bogus")));

        Assert.Contains("status", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // --- Cancel license sale ---

    [Fact]
    public async Task CancelLicenseSaleAsync_SetsCancelledStatus()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(db);

        var created = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest(tenant.Id, LicenseSalePlans.TwelveMonths, null, 100m),
            Guid.Parse(userId));

        var cancelled = await service.CancelLicenseSaleAsync(
            created.Id,
            new CancelLicenseSaleRequest("Customer refund"),
            Guid.Parse(userId));

        Assert.Equal(LicenseSaleStatuses.Cancelled, cancelled.Status);
        var auditRow = await db.BillingAuditLogs
            .OrderByDescending(x => x.TimestampUtc)
            .FirstAsync();
        Assert.Equal(BillingAuditEventTypes.SaleCancelled, auditRow.Action);
        Assert.Contains("Customer refund", auditRow.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelLicenseSaleAsync_MissingReason_Throws()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(db);

        var created = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest(tenant.Id, LicenseSalePlans.SixMonths, null, 50m),
            Guid.Parse(userId));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CancelLicenseSaleAsync(
                created.Id,
                new CancelLicenseSaleRequest("  "),
                Guid.Parse(userId)));

        Assert.Contains("Cancellation reason", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelLicenseSaleAsync_AlreadyCancelled_Throws()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(db);

        var created = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest(tenant.Id, LicenseSalePlans.SixMonths, null, 50m),
            Guid.Parse(userId));

        await service.CancelLicenseSaleAsync(
            created.Id,
            new CancelLicenseSaleRequest("First cancel"),
            Guid.Parse(userId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelLicenseSaleAsync(
                created.Id,
                new CancelLicenseSaleRequest("Second cancel"),
                Guid.Parse(userId)));
    }

    [Fact]
    public async Task CancelLicenseSaleAsync_NotFound_Throws()
    {
        await using var db = CreateDb();
        var userId = SeedUser(db);
        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CancelLicenseSaleAsync(
                Guid.NewGuid(),
                new CancelLicenseSaleRequest("Missing sale"),
                Guid.Parse(userId)));
    }

    // --- PDF generation ---

    [Fact]
    public async Task GenerateInvoicePdfAsync_ProducesValidPdfAndPersistsPath()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"BillingPdf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);

        try
        {
            await using var db = CreateDb();
            var tenant = SeedTenant(db, "cafe");
            var userId = SeedUser(db);
            var service = CreateService(db, contentRoot);

            var sale = await service.CreateLicenseSaleAsync(
                new CreateLicenseSaleRequest(tenant.Id, LicenseSalePlans.TwelveMonths, null, 100m),
                Guid.Parse(userId));

            var pdf = await service.GenerateInvoicePdfAsync(sale.Id);

            Assert.NotEmpty(pdf);
            Assert.Equal(0x25, pdf[0]); // %
            Assert.Equal(0x50, pdf[1]); // P
            Assert.Equal(0x44, pdf[2]); // D
            Assert.Equal(0x46, pdf[3]); // F

            var stored = await db.LicenseSales.IgnoreQueryFilters().AsNoTracking().SingleAsync(s => s.Id == sale.Id);
            Assert.NotNull(stored.InvoicePdfPath);
            Assert.Contains(sale.InvoiceNumber, stored.InvoicePdfPath, StringComparison.Ordinal);

            var absolutePath = Path.Combine(contentRoot, stored.InvoicePdfPath!.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(absolutePath));
        }
        finally
        {
            if (Directory.Exists(contentRoot))
                Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateInvoicePdfAsync_NotFound_Throws()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GenerateInvoicePdfAsync(Guid.NewGuid()));
    }

    // --- License key validation ---

    [Fact]
    public async Task CanExtendLicenseAsync_ReturnsTrueForUnusedValidKey()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var canExtend = await service.CanExtendLicenseAsync("REGK-20261231-cafe-A7F3K2D9");

        Assert.True(canExtend);
    }

    [Theory]
    [InlineData("not-a-key")]
    [InlineData("REGK-ABCDE-BBBBB-CCCCC")]
    [InlineData("")]
    public async Task CanExtendLicenseAsync_RejectsInvalidFormat(string key)
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        Assert.False(await service.CanExtendLicenseAsync(key));
    }

    [Fact]
    public async Task CanExtendLicenseAsync_RejectsReservedBillingKeyAlreadyActiveOnTenant()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        tenant.LicenseKey = "REGK-20261231-cafe-A7F3K2D9";
        tenant.LicenseValidUntilUtc = DateTime.UtcNow.AddMonths(3);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var canExtend = await service.CanExtendLicenseAsync(tenant.LicenseKey!);

        Assert.False(canExtend);
    }

    [Fact]
    public async Task CanExtendLicenseAsync_RejectsActiveSaleRecord()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(db);

        var sale = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest(tenant.Id, LicenseSalePlans.SixMonths, null, 50m),
            Guid.Parse(userId));

        Assert.False(await service.CanExtendLicenseAsync(sale.LicenseKey));
    }

    [Fact]
    public async Task GetLicenseSaleStatsAsync_CountsActiveRevenueOnly()
    {
        await using var db = CreateDb();
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(db);

        var active = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest(tenant.Id, LicenseSalePlans.SixMonths, null, 100m),
            Guid.Parse(userId));

        await service.CancelLicenseSaleAsync(
            active.Id,
            new CancelLicenseSaleRequest("Test cancel"),
            Guid.Parse(userId));

        await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest(tenant.Id, LicenseSalePlans.TwelveMonths, null, 200m),
            Guid.Parse(userId));

        var stats = await service.GetLicenseSaleStatsAsync();

        Assert.Equal(1, stats.TotalSales);
        Assert.Equal(200m, stats.TotalRevenueNet);
        Assert.Equal(240m, stats.TotalRevenueGross);
    }

    private static BillingService CreateService(AppDbContext db, string? contentRootPath = null)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(contentRootPath ?? Path.GetTempPath());

        var audit = new BillingAuditService(db, NullLogger<BillingAuditService>.Instance);

        return new BillingService(
            db,
            new LicenseKeyGenerator(),
            new InvoiceNumberGenerator(db),
            environment.Object,
            Options.Create(new CompanyProfileOptions { CompanyName = "Regkasse Platform" }),
            audit,
            new InvoicePdfGenerator(),
            NullLogger<BillingService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BillingService_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static Tenant SeedTenant(AppDbContext db, string slug, string status = TenantStatuses.Active)
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

    private static string SeedUser(AppDbContext db)
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
