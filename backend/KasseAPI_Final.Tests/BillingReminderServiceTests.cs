using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ReminderServiceTests
{
    [Fact]
    public async Task ScheduleRemindersForSaleAsync_CreatesPendingAnchors()
    {
        var (db, factory) = await CreateDbAsync();
        await using var _ = db;

        var tenant = SeedTenant(db);
        var sale = SeedSale(db, tenant.Id, DateTime.UtcNow.AddDays(45));

        var sut = CreateService(db);
        await sut.ScheduleRemindersForSaleAsync(sale.Id);

        db.ChangeTracker.Clear();
        var reminders = await db.LicenseReminders
            .Where(r => r.LicenseSaleId == sale.Id)
            .OrderBy(r => r.ReminderDateUtc)
            .ToListAsync();

        Assert.Equal(5, reminders.Count);
        Assert.All(reminders, r => Assert.Equal(LicenseReminderStatuses.Pending, r.Status));
    }

    [Fact]
    public async Task CancelRemindersForSaleAsync_CancelsPendingOnly()
    {
        var (db, factory) = await CreateDbAsync();
        await using var _ = db;

        var tenant = SeedTenant(db);
        var sale = SeedSale(db, tenant.Id, DateTime.UtcNow.AddDays(45));
        db.LicenseReminders.Add(new LicenseReminder
        {
            TenantId = tenant.Id,
            LicenseSaleId = sale.Id,
            ReminderDateUtc = DateTime.UtcNow.AddDays(10),
            Status = LicenseReminderStatuses.Pending,
        });
        db.LicenseReminders.Add(new LicenseReminder
        {
            TenantId = tenant.Id,
            LicenseSaleId = sale.Id,
            ReminderDateUtc = DateTime.UtcNow.AddDays(-1),
            Status = LicenseReminderStatuses.Sent,
            ReminderSentAtUtc = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        await sut.CancelRemindersForSaleAsync(sale.Id);

        db.ChangeTracker.Clear();
        var rows = await db.LicenseReminders.Where(r => r.LicenseSaleId == sale.Id).ToListAsync();
        Assert.Equal(LicenseReminderStatuses.Cancelled, rows.Single(r => r.Status != LicenseReminderStatuses.Sent).Status);
        Assert.Equal(LicenseReminderStatuses.Sent, rows.Single(r => r.Status == LicenseReminderStatuses.Sent).Status);
    }

    [Fact]
    public async Task SendPendingRemindersAsync_MarksPendingAsSent()
    {
        var (db, factory) = await CreateDbAsync();
        await using var _ = db;

        var tenant = SeedTenant(db, email: "tenant@regkasse.test");
        var sale = SeedSale(db, tenant.Id, DateTime.UtcNow.AddDays(10));
        db.LicenseReminders.Add(new LicenseReminder
        {
            TenantId = tenant.Id,
            LicenseSaleId = sale.Id,
            ReminderDateUtc = DateTime.UtcNow.AddMinutes(-5),
            Status = LicenseReminderStatuses.Pending,
        });
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        await sut.SendPendingRemindersAsync();
        db.ChangeTracker.Clear();
        var reminder = await db.LicenseReminders.SingleAsync();
        Assert.Equal(LicenseReminderStatuses.Sent, reminder.Status);
        Assert.NotNull(reminder.ReminderSentAtUtc);
    }

    private static ReminderService CreateService(
        AppDbContext db,
        ITenantLicenseService? tenantLicenseService = null)
    {
        return new ReminderService(
            db,
            tenantLicenseService ?? Mock.Of<ITenantLicenseService>(),
            NullLogger<ReminderService>.Instance);
    }

    private static async Task<(AppDbContext Db, IDbContextFactory<AppDbContext> Factory)> CreateDbAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BillingReminder_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
        var factory = TenantTestDoubles.DbContextFactoryForTests(options, NullCurrentTenantAccessor.Instance);
        await db.Database.EnsureCreatedAsync();
        return (db, factory);
    }

    private static Tenant SeedTenant(AppDbContext db, string slug = "dev", string? email = null)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = slug,
            Slug = slug,
            Email = email,
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        db.SaveChanges();
        return tenant;
    }

    private static LicenseSale SeedSale(AppDbContext db, Guid tenantId, DateTime validUntil)
    {
        var sale = new LicenseSale
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LicenseKey = "REGK-20270101-cafe-TESTKEY1",
            LicensePlan = LicenseSalePlans.TwelveMonths,
            ValidFromUtc = DateTime.UtcNow,
            ValidUntilUtc = validUntil,
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
}
