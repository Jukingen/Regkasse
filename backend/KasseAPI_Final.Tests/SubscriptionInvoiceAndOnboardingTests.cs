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
    public async Task GenerateMonthlyInvoicesAsync_creates_invoice_for_active_paid_tenant()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GenerateMonthlyInvoicesAsync_creates_invoice_for_active_paid_tenant))
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));

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
            ValidFromUtc = DateTime.UtcNow.AddMonths(-1),
            ValidUntilUtc = DateTime.UtcNow.AddMonths(11),
            PriceNet = 99,
            VatRate = 20,
            VatAmount = 19.8m,
            PriceGross = 118.8m,
            InvoiceNumber = "INV-1",
            SoldByUserId = Guid.NewGuid(),
            SoldAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());

        var sut = new SubscriptionInvoiceService(
            db,
            env.Object,
            Options.Create(new BillingOptions { MonthlyNetBusiness = 99m }),
            NullLogger<SubscriptionInvoiceService>.Instance);

        var period = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await sut.GenerateMonthlyInvoicesAsync(period);

        Assert.Equal(1, result.Created);
        Assert.Equal(1, await db.SubscriptionInvoices.CountAsync());
        var inv = await db.SubscriptionInvoices.SingleAsync();
        Assert.Equal(LicenseType.Business, inv.LicenseType);
        Assert.Equal(99m, inv.AmountNet);
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
