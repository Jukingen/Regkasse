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
    // --- Prompt 5: core scenario tests ---

    [Fact]
    public async Task PreviewLicenseSale_ValidRequest_ReturnsPreview()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var _ = harness;

        var tenant = await harness.CreateTestTenantAsync();
        var request = new LicenseSalePreviewRequest
        {
            TenantId = tenant.Id,
            LicensePlan = LicenseSalePlans.TwelveMonths,
            PriceNet = 299.00m,
        };

        var result = await harness.CreateBillingService().PreviewLicenseSaleAsync(request);

        Assert.NotNull(result);
        Assert.Equal(tenant.Name, result.TenantName);
        Assert.Equal(365, result.DurationDays);
        Assert.Equal(299.00m, result.PriceNet);
        Assert.Equal(59.80m, result.VatAmount);
        Assert.Equal(358.80m, result.PriceGross);
        Assert.StartsWith("REGK-", result.LicenseKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateLicenseSale_ValidRequest_CreatesSale()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var _ = harness;

        var tenant = await harness.CreateTestTenantAsync();
        var actorUserId = await harness.CreateTestUserAsync();
        var request = new CreateLicenseSaleRequest
        {
            TenantId = tenant.Id,
            LicensePlan = LicenseSalePlans.TwelveMonths,
            PriceNet = 299.00m,
        };

        var result = await harness.CreateBillingService().CreateLicenseSaleAsync(request, actorUserId);

        Assert.NotNull(result);
        Assert.Equal(299.00m, result.PriceNet);
        Assert.Equal(LicenseSaleStatuses.Active, result.Status);
        Assert.StartsWith("RE", result.InvoiceNumber, StringComparison.Ordinal);

        var updatedTenant = await harness.GetTenantAsync(tenant.Id);
        Assert.Equal(result.LicenseKey, updatedTenant.LicenseKey);
        Assert.Equal(result.ValidUntilUtc, updatedTenant.LicenseValidUntilUtc);
        Assert.Equal(result.Id, updatedTenant.CurrentLicenseSaleId);
    }

    [Fact]
    public async Task CancelLicenseSale_ValidRequest_CancelsSale()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var _ = harness;

        var sale = await harness.CreateTestSaleAsync();
        var actorUserId = await harness.CreateTestUserAsync();
        var request = new CancelLicenseSaleRequest
        {
            CancellationReason = "Customer requested cancellation",
        };

        var result = await harness.CreateBillingService()
            .CancelLicenseSaleAsync(sale.Id, request, actorUserId);

        Assert.Equal(LicenseSaleStatuses.Cancelled, result.Status);
        Assert.NotNull(result.CancelledAtUtc);
        Assert.Equal("Customer requested cancellation", result.CancellationReason);
    }

    [Fact]
    public async Task ListLicenseSales_WithFilters_ReturnsFilteredList()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var _ = harness;

        var tenant1 = await harness.CreateTestTenantAsync("tenant-one");
        var tenant2 = await harness.CreateTestTenantAsync("tenant-two");
        await harness.CreateTestSaleAsync(tenant1.Id);
        await harness.CreateTestSaleAsync(tenant2.Id);

        var query = new LicenseSaleListQuery
        {
            TenantId = tenant1.Id,
            PageSize = 10,
        };

        var result = await harness.CreateBillingService().ListLicenseSalesAsync(query);

        Assert.Single(result.Items);
        Assert.Equal(tenant1.Id, result.Items[0].TenantId);
    }

    // --- Extended coverage ---

    [Fact]
    public async Task PreviewLicenseSaleAsync_ComputesTwelveMonthPricing()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var service = CreateService(factory, db);

        var preview = await service.PreviewLicenseSaleAsync(new LicenseSalePreviewRequest
        {
            TenantId = tenant.Id,
            LicensePlan = LicenseSalePlans.TwelveMonths,
            PriceNet = 100m,
            VatRate = 20m,
        });

        Assert.Equal("cafe", preview.TenantSlug);
        Assert.Equal(20m, preview.VatAmount);
        Assert.Equal(120m, preview.PriceGross);
        Assert.Equal("1 Jahr", preview.DurationDisplay);
        Assert.Equal("EUR", preview.Currency);
        Assert.StartsWith("REGK-", preview.LicenseKey, StringComparison.Ordinal);
        Assert.StartsWith("RE", preview.InvoiceNumber, StringComparison.Ordinal);
        Assert.True(new LicenseKeyGenerator().ValidateLicenseKeyFormat(preview.LicenseKey));
    }

    [Fact]
    public async Task PreviewLicenseSaleAsync_CustomPlan_UsesProvidedValidUntil()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var service = CreateService(factory, db);
        var customUntil = DateTime.UtcNow.AddDays(45);

        var preview = await service.PreviewLicenseSaleAsync(new LicenseSalePreviewRequest
        {
            TenantId = tenant.Id,
            LicensePlan = LicenseSalePlans.Custom,
            CustomValidUntilUtc = customUntil,
            PriceNet = 75m,
        });

        Assert.Equal(customUntil.ToUniversalTime().Date, preview.ValidUntilUtc.ToUniversalTime().Date);
        Assert.Equal(15m, preview.VatAmount);
        Assert.Equal(90m, preview.PriceGross);
        Assert.Equal(45, preview.DurationDays);
    }

    [Fact]
    public async Task PreviewLicenseSaleAsync_TenantNotFound_Throws()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var service = CreateService(factory, db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.PreviewLicenseSaleAsync(new LicenseSalePreviewRequest
            {
                TenantId = Guid.NewGuid(),
                LicensePlan = LicenseSalePlans.SixMonths,
                PriceNet = 50m,
            }));
    }

    [Theory]
    [InlineData("invalid_plan")]
    [InlineData("")]
    public async Task PreviewLicenseSaleAsync_InvalidPlan_Throws(string plan)
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var service = CreateService(factory, db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.PreviewLicenseSaleAsync(new LicenseSalePreviewRequest
            {
                TenantId = tenant.Id,
                LicensePlan = plan,
                PriceNet = 50m,
            }));

        Assert.Contains("license plan", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewLicenseSaleAsync_CustomPlanWithoutDate_Throws()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var service = CreateService(factory, db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.PreviewLicenseSaleAsync(new LicenseSalePreviewRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.Custom,
                PriceNet = 50m,
            }));

        Assert.Contains("CustomValidUntilUtc", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewLicenseSaleAsync_NonPositivePrice_Throws()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var service = CreateService(factory, db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.PreviewLicenseSaleAsync(new LicenseSalePreviewRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.SixMonths,
                PriceNet = 0m,
            }));

        Assert.Contains("PriceNet", ex.Message, StringComparison.Ordinal);
    }

    // --- Create license sale ---

    [Fact]
    public async Task CreateLicenseSaleAsync_UpdatesTenantLicenseAndPersistsSale()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        var response = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.SixMonths,
                PriceNet = 50m,
                VatRate = 20m,
                Notes = "Initial sale",
            },
            Guid.Parse(userId));

        Assert.Equal(LicenseSaleStatuses.Active, response.Status);
        Assert.Equal("Initial sale", response.Notes);
        Assert.Equal("Super Admin", response.SoldBy);
        Assert.Equal(10m, response.VatAmount);
        Assert.Equal(60m, response.PriceGross);
        Assert.True(new LicenseKeyGenerator().ValidateLicenseKeyFormat(response.LicenseKey));

        db.ChangeTracker.Clear();
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
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe", TenantStatuses.Deleted);
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateLicenseSaleAsync(
                new CreateLicenseSaleRequest
                {
                    TenantId = tenant.Id,
                    LicensePlan = LicenseSalePlans.SixMonths,
                    PriceNet = 50m,
                },
                Guid.Parse(userId)));
    }

    [Fact]
    public async Task CreateLicenseSaleAsync_UnknownUser_Throws()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var service = CreateService(factory, db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateLicenseSaleAsync(
                new CreateLicenseSaleRequest
                {
                    TenantId = tenant.Id,
                    LicensePlan = LicenseSalePlans.SixMonths,
                    PriceNet = 50m,
                },
                Guid.NewGuid()));

        Assert.Contains("User not found", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateLicenseSaleAsync_CustomPlanBeforeStart_Throws()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateLicenseSaleAsync(
                new CreateLicenseSaleRequest
                {
                    TenantId = tenant.Id,
                    LicensePlan = LicenseSalePlans.Custom,
                    CustomValidUntilUtc = DateTime.UtcNow.AddDays(-1),
                    PriceNet = 50m,
                },
                Guid.Parse(userId)));

        Assert.Contains("CustomValidUntilUtc", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateLicenseSaleAsync_NegativeVatRate_Throws()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateLicenseSaleAsync(
                new CreateLicenseSaleRequest
                {
                    TenantId = tenant.Id,
                    LicensePlan = LicenseSalePlans.SixMonths,
                    PriceNet = 50m,
                    VatRate = -1m,
                },
                Guid.Parse(userId)));

        Assert.Contains("VatRate", ex.Message, StringComparison.Ordinal);
    }

    // --- List with filters ---

    [Fact]
    public async Task ListLicenseSalesAsync_FiltersByTenantStatusAndSearch()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var cafe = SeedTenant(db, "cafe");
        var bar = SeedTenant(db, "bar");
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        var cafeSale = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = cafe.Id,
                LicensePlan = LicenseSalePlans.SixMonths,
                PriceNet = 50m,
                VatRate = 20m,
                Notes = "Cafe note",
            },
            Guid.Parse(userId));
        var barSale = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = bar.Id,
                LicensePlan = LicenseSalePlans.TwelveMonths,
                PriceNet = 100m,
            },
            Guid.Parse(userId));

        await service.CancelLicenseSaleAsync(
            barSale.Id,
            new CancelLicenseSaleRequest { CancellationReason = "Test cancel reason" },
            Guid.Parse(userId));

        var tenantFilter = await service.ListLicenseSalesAsync(
            new LicenseSaleListQuery { TenantId = cafe.Id });
        Assert.Single(tenantFilter.Items);
        Assert.Equal(cafeSale.Id, tenantFilter.Items[0].Id);

        var activeOnly = await service.ListLicenseSalesAsync(
            new LicenseSaleListQuery { Status = LicenseSaleStatuses.Active });
        Assert.Single(activeOnly.Items);
        Assert.Equal(cafeSale.Id, activeOnly.Items[0].Id);

        var searchBySlug = await service.ListLicenseSalesAsync(
            new LicenseSaleListQuery { Search = "cafe" });
        Assert.Single(searchBySlug.Items);
        Assert.Equal(cafeSale.LicenseKey, searchBySlug.Items[0].LicenseKey);

        var searchByInvoice = await service.ListLicenseSalesAsync(
            new LicenseSaleListQuery { Search = cafeSale.InvoiceNumber });
        Assert.Single(searchByInvoice.Items);
    }

    [Fact]
    public async Task ListLicenseSalesAsync_FiltersBySoldDateRange()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        var sale = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.SixMonths,
                PriceNet = 50m,
            },
            Guid.Parse(userId));

        var row = await db.LicenseSales.IgnoreQueryFilters().SingleAsync(s => s.Id == sale.Id);
        row.SoldAtUtc = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var inside = await service.ListLicenseSalesAsync(new LicenseSaleListQuery
        {
            FromDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ToDate = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc),
        });
        Assert.Single(inside.Items);

        var outside = await service.ListLicenseSalesAsync(new LicenseSaleListQuery
        {
            FromDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        Assert.Empty(outside.Items);
    }

    [Fact]
    public async Task ListLicenseSalesAsync_PaginatesResults()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        for (var i = 0; i < 3; i++)
        {
            await service.CreateLicenseSaleAsync(
                new CreateLicenseSaleRequest
                {
                    TenantId = tenant.Id,
                    LicensePlan = LicenseSalePlans.SixMonths,
                    PriceNet = 10m + i,
                },
                Guid.Parse(userId));
        }

        var page1 = await service.ListLicenseSalesAsync(new LicenseSaleListQuery { Page = 1, PageSize = 2 });
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);

        var page2 = await service.ListLicenseSalesAsync(new LicenseSaleListQuery { Page = 2, PageSize = 2 });
        Assert.Single(page2.Items);
    }

    [Fact]
    public async Task ListLicenseSalesAsync_InvalidStatusFilter_Throws()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var service = CreateService(factory, db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ListLicenseSalesAsync(new LicenseSaleListQuery { Status = "bogus" }));

        Assert.Contains("status", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // --- Cancel license sale ---

    [Fact]
    public async Task CancelLicenseSaleAsync_SetsCancelledStatus()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        var created = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.TwelveMonths,
                PriceNet = 100m,
            },
            Guid.Parse(userId));

        var cancelled = await service.CancelLicenseSaleAsync(
            created.Id,
            new CancelLicenseSaleRequest { CancellationReason = "Customer refund request" },
            Guid.Parse(userId));

        Assert.Equal(LicenseSaleStatuses.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.CancelledAtUtc);
        Assert.Equal("Customer refund request", cancelled.CancellationReason);
        var auditRow = await db.BillingAuditLogs
            .OrderByDescending(x => x.TimestampUtc)
            .FirstAsync();
        Assert.Equal(BillingAuditEventTypes.SaleCancelled, auditRow.Action);
        Assert.Contains("Customer refund", auditRow.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelLicenseSaleAsync_MissingReason_Throws()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        var created = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.SixMonths,
                PriceNet = 50m,
            },
            Guid.Parse(userId));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CancelLicenseSaleAsync(
                created.Id,
                new CancelLicenseSaleRequest { CancellationReason = "  " },
                Guid.Parse(userId)));

        Assert.Contains("Cancellation reason", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelLicenseSaleAsync_AlreadyCancelled_Throws()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        var created = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.SixMonths,
                PriceNet = 50m,
            },
            Guid.Parse(userId));

        await service.CancelLicenseSaleAsync(
            created.Id,
            new CancelLicenseSaleRequest { CancellationReason = "First cancel reason" },
            Guid.Parse(userId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelLicenseSaleAsync(
                created.Id,
                new CancelLicenseSaleRequest { CancellationReason = "Second cancel reason" },
                Guid.Parse(userId)));
    }

    [Fact]
    public async Task CancelLicenseSaleAsync_NotFound_Throws()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CancelLicenseSaleAsync(
                Guid.NewGuid(),
                new CancelLicenseSaleRequest { CancellationReason = "Missing sale reason" },
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
            var (db, factory) = CreateDb();
        await using var _db = db;
            var tenant = SeedTenant(db, "cafe");
            var userId = SeedUser(db);
            var service = CreateService(factory, db, contentRoot);

            var sale = await service.CreateLicenseSaleAsync(
                new CreateLicenseSaleRequest
                {
                    TenantId = tenant.Id,
                    LicensePlan = LicenseSalePlans.TwelveMonths,
                    PriceNet = 100m,
                },
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
        var (db, factory) = CreateDb();
        await using var _db = db;
        var service = CreateService(factory, db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GenerateInvoicePdfAsync(Guid.NewGuid()));
    }

    // --- License key validation ---

    [Fact]
    public async Task IsLicenseKeyValidAsync_ReturnsTrueForUnusedValidKey()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var service = CreateService(factory, db);

        Assert.True(await service.IsLicenseKeyValidAsync("REGK-20261231-cafe-A7F3K2D9"));
    }

    [Theory]
    [InlineData("not-a-key")]
    [InlineData("REGK-ABCDE-BBBBB-CCCCC")]
    [InlineData("")]
    public async Task IsLicenseKeyValidAsync_RejectsInvalidFormat(string key)
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var service = CreateService(factory, db);

        Assert.False(await service.IsLicenseKeyValidAsync(key));
    }

    [Fact]
    public async Task IsLicenseKeyValidAsync_RejectsReservedBillingKeyAlreadyActiveOnTenant()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        tenant.LicenseKey = "REGK-20261231-cafe-A7F3K2D9";
        tenant.LicenseValidUntilUtc = DateTime.UtcNow.AddMonths(3);
        await db.SaveChangesAsync();

        var service = CreateService(factory, db);

        Assert.False(await service.IsLicenseKeyValidAsync(tenant.LicenseKey!));
    }

    [Fact]
    public async Task IsLicenseKeyValidAsync_RejectsActiveSaleRecord()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        var sale = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.SixMonths,
                PriceNet = 50m,
            },
            Guid.Parse(userId));

        Assert.False(await service.IsLicenseKeyValidAsync(sale.LicenseKey));
    }

    [Fact]
    public async Task GetSaleByLicenseKeyAsync_ReturnsSaleWhenFound()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        var created = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.SixMonths,
                PriceNet = 50m,
            },
            Guid.Parse(userId));

        var found = await service.GetSaleByLicenseKeyAsync(created.LicenseKey);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
    }

    [Fact]
    public async Task GetSaleByLicenseKeyAsync_ReturnsNullWhenMissing()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var service = CreateService(factory, db);

        Assert.Null(await service.GetSaleByLicenseKeyAsync("REGK-20261231-cafe-A7F3K2D9"));
    }

    [Fact]
    public async Task GetNextInvoiceNumberAsync_ReturnsFormattedNumber()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var service = CreateService(factory, db);

        var number = await service.GetNextInvoiceNumberAsync(new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("RE202608", number, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLicenseSaleStatsAsync_CountsActiveRevenueOnly()
    {
        var (db, factory) = CreateDb();
        await using var _db = db;
        var tenant = SeedTenant(db, "cafe");
        var userId = SeedUser(db);
        var service = CreateService(factory, db);

        var active = await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.SixMonths,
                PriceNet = 100m,
            },
            Guid.Parse(userId));

        await service.CancelLicenseSaleAsync(
            active.Id,
            new CancelLicenseSaleRequest { CancellationReason = "Test cancel reason" },
            Guid.Parse(userId));

        await service.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.TwelveMonths,
                PriceNet = 200m,
            },
            Guid.Parse(userId));

        var stats = await service.GetLicenseSaleStatsAsync();

        Assert.Equal(1, stats.TotalSales);
        Assert.Equal(200m, stats.TotalRevenueNet);
        Assert.Equal(240m, stats.TotalRevenueGross);
        Assert.Equal(1, stats.CancelledSales);
        Assert.Equal(200m, stats.AveragePriceNet);
        Assert.Equal(1, stats.TotalTenantsWithLicense);
    }

    private static BillingService CreateService(IDbContextFactory<AppDbContext> factory, AppDbContext auditDb, string? contentRootPath = null)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(contentRootPath ?? Path.GetTempPath());

        var audit = new BillingAuditService(auditDb, NullLogger<BillingAuditService>.Instance);

        return new BillingService(
            factory,
            new LicenseKeyGenerator(),
            audit,
            environment.Object,
            Options.Create(new CompanyProfileOptions { CompanyName = "Regkasse Platform" }),
            new InvoicePdfGenerator(),
            NullLogger<BillingService>.Instance);
    }

    private static (AppDbContext Db, IDbContextFactory<AppDbContext> Factory) CreateDb()
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

    /// <summary>Shared in-memory DB + factory for spec-style billing tests.</summary>
    private sealed class BillingServiceTestHarness : IAsyncDisposable
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
            var dbName = $"BillingServiceHarness_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
            var factory = TenantTestDoubles.DbContextFactoryForTests(options, NullCurrentTenantAccessor.Instance);
            return Task.FromResult(new BillingServiceTestHarness(db, factory));
        }

        public BillingService CreateBillingService()
        {
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());

            return new BillingService(
                _factory,
                new LicenseKeyGenerator(),
                new BillingAuditService(_db, NullLogger<BillingAuditService>.Instance),
                environment.Object,
                Options.Create(new CompanyProfileOptions { CompanyName = "Regkasse Platform" }),
                new InvoicePdfGenerator(),
                NullLogger<BillingService>.Instance);
        }

        public async Task<Tenant> CreateTestTenantAsync(string slug = "cafe")
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

        public ValueTask DisposeAsync() => _db.DisposeAsync();
    }
}
