using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Hosted;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        var (db, _) = await CreateDbAsync();
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

    [Fact]
    public async Task CheckAndCreateRemindersAsync_CreatesExactDayAnchor_AndSkipsDuplicates()
    {
        var (db, _) = await CreateDbAsync();
        await using var _ = db;

        var tenant = SeedTenant(db);
        var validUntil = DateTime.UtcNow.AddDays(7);
        var sale = SeedSale(db, tenant.Id, validUntil);
        var expectedDays = (int)Math.Ceiling((validUntil - DateTime.UtcNow).TotalDays);

        var tenantLicense = new Mock<ITenantLicenseService>();
        tenantLicense
            .Setup(x => x.GetExpiringLicensesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ExpiringLicenseInfo
                {
                    TenantId = tenant.Id,
                    TenantName = tenant.Name,
                    TenantSlug = tenant.Slug,
                    LicenseKey = sale.LicenseKey,
                    ValidUntilUtc = validUntil,
                    DaysRemaining = expectedDays,
                    LicenseSaleId = sale.Id,
                    TenantEmail = tenant.Email,
                },
            ]);

        var sut = CreateService(
            db,
            tenantLicense.Object,
            new BillingOptions { ReminderDaysBeforeExpiry = [expectedDays] });
        await sut.CheckAndCreateRemindersAsync();
        await sut.CheckAndCreateRemindersAsync();

        db.ChangeTracker.Clear();
        var reminders = await db.LicenseReminders.Where(r => r.LicenseSaleId == sale.Id).ToListAsync();
        Assert.Single(reminders);
        Assert.Equal(validUntil.Date.AddDays(-expectedDays), reminders[0].ReminderDateUtc);
        Assert.Equal(LicenseReminderStatuses.Pending, reminders[0].Status);
    }

    [Fact]
    public void BillingReminderHostedService_ComputeDelayUntilUtc_IsPositive()
    {
        var delay = BillingReminderHostedService.ComputeDelayUntilUtc(
            DateTime.UtcNow.Hour,
            (DateTime.UtcNow.Minute + 2) % 60);
        Assert.True(delay > TimeSpan.Zero);
        Assert.True(delay <= TimeSpan.FromDays(1));
    }

    [Fact]
    public void LicenseReminderEmailComposer_IncludesLicenseTypeInBodies()
    {
        var model = LicenseReminderEmailComposer.CreateModel(
            "Cafe Muster",
            7,
            DateTime.UtcNow.Date.AddDays(7),
            licenseType: LicenseType.Business);
        var html = LicenseReminderEmailComposer.BuildHtmlBody(model);
        var plain = LicenseReminderEmailComposer.BuildPlainBody(model);

        Assert.Contains("Business", html, StringComparison.Ordinal);
        Assert.Contains("Paket:", plain, StringComparison.Ordinal);
        Assert.Contains("Business", plain, StringComparison.Ordinal);
    }

    private static ReminderService CreateService(
        AppDbContext db,
        ITenantLicenseService? tenantLicenseService = null,
        BillingOptions? billingOptions = null)
    {
        return new ReminderService(
            db,
            tenantLicenseService ?? Mock.Of<ITenantLicenseService>(),
            Options.Create(billingOptions ?? new BillingOptions
            {
                ReminderDaysBeforeExpiry = [30, 15, 7, 3, 1],
            }),
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
            LicenseType = LicenseType.Starter,
            ValidFromUtc = DateTime.UtcNow,
            ValidUntilUtc = validUntil,
            PriceNet = 100m,
            VatRate = 20m,
            VatAmount = 20m,
            PriceGross = 120m,
            Currency = "EUR",
            SoldAtUtc = DateTime.UtcNow,
            SoldByUserId = Guid.NewGuid(),
            InvoiceNumber = $"RE{Guid.NewGuid():N}"[..16],
            Status = LicenseSaleStatuses.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.LicenseSales.Add(sale);
        db.SaveChanges();
        return sale;
    }
}
