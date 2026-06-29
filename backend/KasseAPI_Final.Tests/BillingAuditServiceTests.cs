using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BillingAuditServiceTests
{
    [Fact]
    public async Task LogAsync_CreatesAuditEntry()
    {
        var (db, factory) = CreateDb();
        await using var _ = db;

        var service = CreateAuditService(db);
        var tenant = CreateTestTenant(db);
        var sale = CreateTestSale(db, tenant.Id);

        await service.LogAsync(
            "TEST_ACTION",
            tenant.Id,
            sale.Id,
            "{\"test\":\"data\"}",
            "127.0.0.1");

        var logs = await service.GetForSaleAsync(sale.Id);

        Assert.Single(logs);
        Assert.Equal("TEST_ACTION", logs.First().Action);
        Assert.Equal(tenant.Id, logs.First().TenantId);
    }

    [Fact]
    public async Task LogAsync_PersistsAuditRow()
    {
        var (db, factory) = CreateDb();
        await using var _ = db;

        var tenant = SeedTenant(db);
        var actorUserId = SeedUser(db);
        var saleId = Guid.NewGuid();
        var sut = BillingTestDoubles.CreateAuditService(db);

        await sut.LogAsync(
            BillingAuditEventTypes.SaleCreated,
            actorUserId,
            tenant.Id,
            saleId,
            details: "{\"priceGross\":120}");

        db.ChangeTracker.Clear();
        var row = await db.BillingAuditLogs.SingleAsync();
        Assert.Equal(BillingAuditEventTypes.SaleCreated, row.Action);
        Assert.Equal(actorUserId, row.UserId);
        Assert.Equal(tenant.Id, row.TenantId);
        Assert.Equal(saleId, row.SaleId);
    }

    [Fact]
    public async Task ListAsync_FiltersByTenantAndAction()
    {
        var (db, factory) = CreateDb();
        await using var _ = db;

        var tenantA = SeedTenant(db, "cafe-a");
        var tenantB = SeedTenant(db, "cafe-b");
        var actorUserId = SeedUser(db);
        var sut = BillingTestDoubles.CreateAuditService(db);

        await sut.LogAsync(BillingAuditEventTypes.SaleCreated, actorUserId, tenantA.Id, Guid.NewGuid());
        await sut.LogAsync(BillingAuditEventTypes.SaleCancelled, actorUserId, tenantA.Id, Guid.NewGuid());
        await sut.LogAsync(BillingAuditEventTypes.SaleCreated, actorUserId, tenantB.Id, Guid.NewGuid());

        var result = await sut.ListAsync(new BillingAuditLogQuery
        {
            TenantId = tenantA.Id,
            Action = BillingAuditEventTypes.SaleCreated,
        });

        Assert.Single(result.Items);
        Assert.Equal(BillingAuditEventTypes.SaleCreated, result.Items[0].Action);
        Assert.Equal(tenantA.Id, result.Items[0].TenantId);
    }

    [Fact]
    public async Task GetForSaleAsync_ReturnsSaleAuditTrail()
    {
        var (db, factory) = CreateDb();
        await using var _ = db;

        var tenant = SeedTenant(db);
        var actorUserId = SeedUser(db);
        var saleId = Guid.NewGuid();
        var sut = BillingTestDoubles.CreateAuditService(db);

        await sut.LogAsync(BillingAuditEventTypes.LicenseActivated, actorUserId, tenant.Id, saleId);
        await sut.LogAsync(BillingAuditEventTypes.LicenseExtended, actorUserId, tenant.Id, saleId);
        await sut.LogAsync(BillingAuditEventTypes.SaleCreated, actorUserId, tenant.Id, Guid.NewGuid());

        var rows = await sut.GetForSaleAsync(saleId);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(saleId, r.SaleId));
    }

    private static BillingAuditService CreateAuditService(AppDbContext db) =>
        BillingTestDoubles.CreateAuditService(db);

    private static Tenant CreateTestTenant(AppDbContext db) => SeedTenant(db);

    private static LicenseSale CreateTestSale(AppDbContext db, Guid tenantId)
    {
        var sale = new LicenseSale
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LicenseKey = "REGK-20270101-cafe-TESTKEY1",
            LicensePlan = LicenseSalePlans.TwelveMonths,
            ValidFromUtc = DateTime.UtcNow,
            ValidUntilUtc = DateTime.UtcNow.AddDays(365),
            PriceNet = 100m,
            VatRate = 20m,
            VatAmount = 20m,
            PriceGross = 120m,
            Currency = "EUR",
            InvoiceNumber = "RE-2026-00001",
            Status = LicenseSaleStatuses.Active,
            SoldAtUtc = DateTime.UtcNow,
            SoldByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.LicenseSales.Add(sale);
        db.SaveChanges();
        return sale;
    }

    private static (AppDbContext Db, IDbContextFactory<AppDbContext> Factory) CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BillingAudit_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
        var factory = TenantTestDoubles.DbContextFactoryForTests(options, NullCurrentTenantAccessor.Instance);
        db.Database.EnsureCreated();
        return (db, factory);
    }

    private static Tenant SeedTenant(AppDbContext db, string slug = "cafe")
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = slug,
            Slug = slug,
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        db.SaveChanges();
        return tenant;
    }

    private static Guid SeedUser(AppDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = userId.ToString("D"),
            UserName = "audit-user",
            NormalizedUserName = "AUDIT-USER",
            Email = "audit@regkasse.test",
            NormalizedEmail = "AUDIT@REGKASSE.TEST",
            FirstName = "Audit",
            LastName = "User",
        });
        db.SaveChanges();
        return userId;
    }
}
