using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Onboarding;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class SubscriptionInvoiceServiceTests
{
    [Fact]
    public async Task GenerateMonthlyInvoicesAsync_creates_invoice_when_prepaid_skip_disabled()
    {
        await using var db = CreateDb(nameof(GenerateMonthlyInvoicesAsync_creates_invoice_when_prepaid_skip_disabled));
        var tenantId = await SeedTenantWithPrepaidLicenseAsync(db);

        var sut = CreateSut(db, new BillingOptions
        {
            MonthlyNetBusiness = 99m,
            SkipPrepaidTenants = false,
            SkipTrialTenants = true,
        });

        var period = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await sut.GenerateMonthlyInvoicesAsync(period);

        Assert.Equal(1, result.Created);
        Assert.Equal(1, await db.SubscriptionInvoices.CountAsync());
        var inv = await db.SubscriptionInvoices.SingleAsync();
        Assert.Equal(LicenseType.Business, inv.LicenseType);
        Assert.Equal(99m, inv.AmountNet);
        Assert.Equal(SubscriptionInvoiceStatuses.Issued, inv.Status);
    }

    [Fact]
    public async Task GenerateMonthlyInvoicesAsync_skips_tenant_with_prepaid_license_covering_period()
    {
        await using var db = CreateDb(nameof(GenerateMonthlyInvoicesAsync_skips_tenant_with_prepaid_license_covering_period));
        await SeedTenantWithPrepaidLicenseAsync(db);

        var sut = CreateSut(db, new BillingOptions
        {
            MonthlyNetBusiness = 99m,
            SkipPrepaidTenants = true,
        });

        var period = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await sut.GenerateMonthlyInvoicesAsync(period);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, await db.SubscriptionInvoices.CountAsync());
    }

    [Fact]
    public async Task GenerateMonthlyInvoicesAsync_skips_open_trial_tenant()
    {
        await using var db = CreateDb(nameof(GenerateMonthlyInvoicesAsync_skips_open_trial_tenant));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Trial Cafe",
            Slug = "trial-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            TrialStatus = TrialStatuses.Active,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new BillingOptions
        {
            MonthlyNetStarter = 49m,
            SkipPrepaidTenants = true,
            SkipTrialTenants = true,
        });

        var period = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await sut.GenerateMonthlyInvoicesAsync(period);

        Assert.Equal(0, result.Created);
        Assert.True(result.Skipped >= 1);
        Assert.Equal(0, await db.SubscriptionInvoices.CountAsync());
    }

    [Fact]
    public async Task GenerateMonthlyInvoicesAsync_creates_invoice_for_tenant_without_prepaid_cover()
    {
        await using var db = CreateDb(nameof(GenerateMonthlyInvoicesAsync_creates_invoice_for_tenant_without_prepaid_cover));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Payg",
            Slug = "payg",
            Email = "payg@example.com",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new BillingOptions
        {
            MonthlyNetStarter = 49m,
            SkipPrepaidTenants = true,
            SkipTrialTenants = true,
        });

        var period = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await sut.GenerateMonthlyInvoicesAsync(period);

        Assert.Equal(1, result.Created);
        var inv = await db.SubscriptionInvoices.SingleAsync();
        Assert.Equal(49m, inv.AmountNet);
        Assert.Equal(LicenseType.Starter, inv.LicenseType);
    }

    [Fact]
    public async Task MarkPaidAsync_sets_paid_fields()
    {
        await using var db = CreateDb(nameof(MarkPaidAsync_sets_paid_fields));
        var invoice = await SeedIssuedInvoiceAsync(db);
        var sut = CreateSut(db);
        var actor = Guid.NewGuid();
        var paidAt = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

        var result = await sut.MarkPaidAsync(
            invoice.Id,
            new MarkPaidRequest
            {
                PaidAt = paidAt,
                PaymentMethod = SubscriptionInvoicePaymentMethods.BankTransfer,
                Reference = "AT-REF-1",
            },
            actor);

        Assert.True(result.Succeeded);
        Assert.Equal(SubscriptionInvoiceStatuses.Paid, result.Invoice!.Status);
        Assert.Equal(paidAt, result.Invoice.PaidAtUtc);
        Assert.Equal(SubscriptionInvoicePaymentMethods.BankTransfer, result.Invoice.PaymentMethod);
        Assert.Equal("AT-REF-1", result.Invoice.PaymentReference);

        var stored = await db.SubscriptionInvoices.SingleAsync();
        Assert.Equal(SubscriptionInvoiceStatuses.Paid, stored.Status);
        Assert.Equal("AT-REF-1", stored.PaymentReference);
    }

    [Fact]
    public async Task MarkPaidAsync_rejects_already_paid()
    {
        await using var db = CreateDb(nameof(MarkPaidAsync_rejects_already_paid));
        var invoice = await SeedIssuedInvoiceAsync(db);
        invoice.Status = SubscriptionInvoiceStatuses.Paid;
        invoice.PaidAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var result = await sut.MarkPaidAsync(invoice.Id, new MarkPaidRequest(), Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(SubscriptionInvoiceService.AlreadyPaidCode, result.Code);
    }

    [Fact]
    public async Task VoidAsync_voids_issued_invoice()
    {
        await using var db = CreateDb(nameof(VoidAsync_voids_issued_invoice));
        var invoice = await SeedIssuedInvoiceAsync(db);
        var sut = CreateSut(db);

        var result = await sut.VoidAsync(
            invoice.Id,
            new VoidInvoiceRequest { Reason = "Duplicate billing" },
            Guid.NewGuid());

        Assert.True(result.Succeeded);
        Assert.Equal(SubscriptionInvoiceStatuses.Void, result.Invoice!.Status);
        Assert.Equal("Duplicate billing", result.Invoice.VoidReason);
        Assert.NotNull(result.Invoice.VoidedAtUtc);
    }

    [Fact]
    public async Task VoidAsync_rejects_paid_invoice()
    {
        await using var db = CreateDb(nameof(VoidAsync_rejects_paid_invoice));
        var invoice = await SeedIssuedInvoiceAsync(db);
        invoice.Status = SubscriptionInvoiceStatuses.Paid;
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var result = await sut.VoidAsync(
            invoice.Id,
            new VoidInvoiceRequest { Reason = "oops" },
            Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(SubscriptionInvoiceService.PaidCannotVoidCode, result.Code);
    }

    [Fact]
    public async Task ShouldGenerateInvoiceAsync_false_when_prepaid_covers_period()
    {
        await using var db = CreateDb(nameof(ShouldGenerateInvoiceAsync_false_when_prepaid_covers_period));
        var tenantId = await SeedTenantWithPrepaidLicenseAsync(db);
        var sut = CreateSut(db, new BillingOptions { SkipPrepaidTenants = true, SkipTrialTenants = true });
        var periodStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.False(await sut.ShouldGenerateInvoiceAsync(tenantId, periodStart, periodStart.AddMonths(1)));
    }

    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
    }

    private static SubscriptionInvoiceService CreateSut(AppDbContext db, BillingOptions? billing = null)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());
        return new SubscriptionInvoiceService(
            db,
            env.Object,
            Options.Create(billing ?? new BillingOptions()),
            NullLogger<SubscriptionInvoiceService>.Instance);
    }

    private static async Task<Guid> SeedTenantWithPrepaidLicenseAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.LicenseSales.Add(new LicenseSale
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LicenseKey = "REGK-TEST",
            LicensePlan = LicenseSalePlans.TwelveMonths,
            LicenseType = LicenseType.Business,
            Status = LicenseSaleStatuses.Active,
            ValidFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ValidUntilUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PriceNet = 99,
            VatRate = 20,
            VatAmount = 19.8m,
            PriceGross = 118.8m,
            InvoiceNumber = "INV-1",
            SoldByUserId = Guid.NewGuid(),
            SoldAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private static async Task<SubscriptionInvoice> SeedIssuedInvoiceAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe",
            Email = "cafe@example.com",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var invoice = new SubscriptionInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "SUB-202607-CAFE-ABC123",
            PeriodStartUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            LicenseType = LicenseType.Starter,
            AmountNet = 49m,
            VatRate = 20m,
            AmountVat = 9.80m,
            AmountGross = 58.80m,
            Currency = "EUR",
            Status = SubscriptionInvoiceStatuses.Issued,
            IssuedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
        db.SubscriptionInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }
}

public class TenantOnboardingChecklistServiceTests
{
    [Fact]
    public async Task EnsureAndGetAsync_seeds_default_steps_with_AccountCreated_done()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(EnsureAndGetAsync_seeds_default_steps_with_AccountCreated_done))
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "T",
            Slug = "t",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var email = new Mock<KasseAPI_Final.Services.Email.IEmailService>();
        var sut = new TenantOnboardingChecklistService(db, email.Object, NullLogger<TenantOnboardingChecklistService>.Instance);

        var overview = await sut.EnsureAndGetAsync(tenantId);

        Assert.Equal(4, overview.TotalCount);
        Assert.Equal(1, overview.CompletedCount);
        Assert.True(overview.Steps.First(s => s.Step == TenantOnboardingSteps.AccountCreated).IsCompleted);
        Assert.False(overview.Steps.First(s => s.Step == TenantOnboardingSteps.ProductsImported).IsCompleted);
    }

    [Fact]
    public async Task CompleteStepAsync_marks_step_and_sends_email()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(CompleteStepAsync_marks_step_and_sends_email))
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "T2",
            Slug = "t2",
            Email = "admin@t2.test",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var email = new Mock<KasseAPI_Final.Services.Email.IEmailService>();
        email.Setup(e => e.TrySendHtmlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new TenantOnboardingChecklistService(db, email.Object, NullLogger<TenantOnboardingChecklistService>.Instance);
        await sut.EnsureAndGetAsync(tenantId);

        var overview = await sut.CompleteStepAsync(tenantId, TenantOnboardingSteps.ProductsImported, "user-1");

        Assert.Equal(2, overview.CompletedCount);
        email.Verify(
            e => e.TrySendHtmlAsync("admin@t2.test", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
