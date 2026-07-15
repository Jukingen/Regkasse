using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseReminderServiceTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(15)]
    [InlineData(7)]
    [InlineData(3)]
    [InlineData(1)]
    public async Task SendDueMandantExpiryRemindersAsync_SendsAtAnchorDays(int anchorDays)
    {
        var (db, _) = await CreateDbAsync();
        await using var _ = db;

        var tenant = SeedTenant(db, validUntilUtc: DateTime.UtcNow.AddDays(anchorDays));
        SeedOwnerUser(db, tenant.Id, "owner@regkasse.test");

        var emailSender = new Mock<ILicenseReminderEmailSender>();
        emailSender
            .Setup(x => x.TrySendTenantLicenseReminderAsync(
                "owner@regkasse.test",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var billingAudit = new Mock<IBillingAuditService>();
        billingAudit
            .Setup(x => x.LogAsync(
                BillingAuditEventTypes.LicenseReminderSent,
                Guid.Empty,
                tenant.Id,
                null,
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateService(db, emailSender.Object, billingAudit.Object);
        var result = await sut.SendDueMandantExpiryRemindersAsync();

        Assert.Equal(1, result.EmailsSent);
        emailSender.Verify(
            x => x.TrySendTenantLicenseReminderAsync(
                "owner@regkasse.test",
                It.Is<string>(s => s.Contains(anchorDays.ToString())),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendDueMandantExpiryRemindersAsync_IsIdempotentPerAnchor()
    {
        var (db, _) = await CreateDbAsync();
        await using var _ = db;

        var validUntil = DateTime.UtcNow.AddDays(7);
        var tenant = SeedTenant(db, validUntilUtc: validUntil);
        SeedOwnerUser(db, tenant.Id, "owner@regkasse.test");

        var dedupKey = $"{tenant.Id:N}_{validUntil:yyyyMMdd}_7";
        db.BillingAuditLogs.Add(new BillingAuditLog
        {
            TenantId = tenant.Id,
            UserId = Guid.Empty,
            Action = BillingAuditEventTypes.LicenseReminderSent,
            Details = $"{{\"dedupKey\":\"{dedupKey}\"}}",
            TimestampUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var emailSender = new Mock<ILicenseReminderEmailSender>();
        var sut = CreateService(db, emailSender.Object, Mock.Of<IBillingAuditService>());
        var result = await sut.SendDueMandantExpiryRemindersAsync();

        Assert.Equal(0, result.EmailsSent);
        Assert.True(result.Skipped >= 1);
        emailSender.Verify(
            x => x.TrySendTenantLicenseReminderAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendDueBillingSaleRemindersAsync_SendsAndMarksSent()
    {
        var (db, _) = await CreateDbAsync();
        await using var _ = db;

        var tenant = SeedTenant(db, validUntilUtc: DateTime.UtcNow.AddDays(10), email: "tenant@regkasse.test");
        var sale = SeedSale(db, tenant.Id, DateTime.UtcNow.AddDays(10));
        db.LicenseReminders.Add(new LicenseReminder
        {
            TenantId = tenant.Id,
            LicenseSaleId = sale.Id,
            ReminderDateUtc = DateTime.UtcNow.AddDays(-1).Date,
            Status = LicenseReminderStatuses.Pending,
        });
        await db.SaveChangesAsync();

        var emailSender = new Mock<ILicenseReminderEmailSender>();
        emailSender
            .Setup(x => x.TrySendTenantLicenseReminderAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = CreateService(db, emailSender.Object, Mock.Of<IBillingAuditService>());
        var sent = await sut.SendDueBillingSaleRemindersAsync();

        Assert.Equal(1, sent);
        db.ChangeTracker.Clear();
        var reminder = await db.LicenseReminders.SingleAsync();
        Assert.Equal(LicenseReminderStatuses.Sent, reminder.Status);
        Assert.NotNull(reminder.ReminderSentAtUtc);
    }

    private static LicenseReminderService CreateService(
        AppDbContext db,
        ILicenseReminderEmailSender emailSender,
        IBillingAuditService billingAudit)
    {
        return new LicenseReminderService(
            db,
            emailSender,
            billingAudit,
            Options.Create(new LicenseOptions { ReminderDays = [30, 15, 7, 3, 1] }),
            NullLogger<LicenseReminderService>.Instance);
    }

    private static async Task<(AppDbContext Db, IDbContextFactory<AppDbContext> Factory)> CreateDbAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LicenseReminder_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
        var factory = TenantTestDoubles.DbContextFactoryForTests(options, NullCurrentTenantAccessor.Instance);
        await db.Database.EnsureCreatedAsync();
        return (db, factory);
    }

    private static Tenant SeedTenant(
        AppDbContext db,
        DateTime? validUntilUtc = null,
        string slug = "cafe",
        string? email = null)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Cafe Demo",
            Slug = slug,
            Email = email,
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseValidUntilUtc = validUntilUtc,
            LicenseKey = "REGK-20270101-cafe-TESTKEY1",
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        db.SaveChanges();
        return tenant;
    }

    private static void SeedOwnerUser(AppDbContext db, Guid tenantId, string email)
    {
        var userId = Guid.NewGuid().ToString();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
        });
        db.UserTenantMemberships.Add(new UserTenantMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            IsActive = true,
            IsOwner = true,
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.SaveChanges();
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
