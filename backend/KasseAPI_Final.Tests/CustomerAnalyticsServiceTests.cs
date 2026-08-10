using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services.Analytics;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class CustomerAnalyticsServiceTests
{
    [Fact]
    public async Task GetCustomerAnalytics_computes_status_license_and_mrr()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(null);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetCustomerAnalytics_computes_status_license_and_mrr))
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);

        var activeId = Guid.NewGuid();
        var onboardId = Guid.NewGuid();
        var suspendedId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Tenants.AddRange(
            new Tenant
            {
                Id = activeId,
                Name = "Active",
                Slug = "active-a",
                Status = TenantStatuses.Active,
                IsActive = true,
                LicenseValidUntilUtc = now.AddDays(3),
                CreatedAt = now.AddDays(-10),
            },
            new Tenant
            {
                Id = onboardId,
                Name = "Onboard",
                Slug = "onboard-b",
                Status = TenantStatuses.InOnboarding,
                IsActive = true,
                CreatedAt = now.AddDays(-2),
            },
            new Tenant
            {
                Id = suspendedId,
                Name = "Suspended",
                Slug = "susp-c",
                Status = TenantStatuses.Suspended,
                IsActive = false,
                LicenseValidUntilUtc = now.AddDays(-2),
                CreatedAt = now.AddDays(-40),
            });

        db.LicenseSales.AddRange(
            new LicenseSale
            {
                Id = Guid.NewGuid(),
                TenantId = activeId,
                LicenseKey = "REGK-TEST-ACTIVE",
                LicensePlan = LicenseSalePlans.TwelveMonths,
                LicenseType = LicenseType.Business,
                ValidFromUtc = now.AddMonths(-1),
                ValidUntilUtc = now.AddMonths(11),
                PriceNet = 1200m,
                VatAmount = 240m,
                PriceGross = 1440m,
                Status = LicenseSaleStatuses.Active,
                SoldByUserId = Guid.NewGuid(),
                InvoiceNumber = "INV-1",
            },
            new LicenseSale
            {
                Id = Guid.NewGuid(),
                TenantId = onboardId,
                LicenseKey = "REGK-TEST-TRIAL",
                LicensePlan = LicenseSalePlans.Custom,
                LicenseType = LicenseType.Trial,
                ValidFromUtc = now,
                ValidUntilUtc = now.AddDays(30),
                PriceNet = 0m,
                VatAmount = 0m,
                PriceGross = 0m,
                Status = LicenseSaleStatuses.Active,
                SoldByUserId = Guid.NewGuid(),
                InvoiceNumber = "INV-2",
            });
        await db.SaveChangesAsync();

        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<CustomerAnalyticsDto>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<CustomerAnalyticsDto>>, TimeSpan?, CancellationToken>(
                (_, factory, _, ct) => factory(ct));

        var sut = new CustomerAnalyticsService(db, cache.Object, NullLogger<CustomerAnalyticsService>.Instance);
        var dto = await sut.GetCustomerAnalyticsAsync();

        Assert.Equal(3, dto.TotalTenants);
        Assert.Equal(1, dto.ActiveTenants);
        Assert.Equal(1, dto.InOnboardingTenants);
        Assert.Equal(1, dto.SuspendedTenants);
        Assert.Equal(1, dto.TrialTenants);
        Assert.Equal(1, dto.PaidTenants);
        Assert.Equal(1, dto.ExpiringSoon);
        Assert.Equal(1, dto.ExpiredTenants);
        Assert.Equal(100m, dto.Mrr); // 1200 / 12
        Assert.Equal(2, dto.NewTenantsLast30Days);
    }

    [Theory]
    [InlineData(LicenseSalePlans.SixMonths, 600, 100)]
    [InlineData(LicenseSalePlans.TwelveMonths, 1200, 100)]
    public void ToMonthlyRecurring_divides_by_plan_months(string plan, decimal net, decimal expected)
    {
        var now = DateTime.UtcNow;
        var monthly = CustomerAnalyticsService.ToMonthlyRecurring(net, plan, now, now.AddMonths(12));
        Assert.Equal(expected, monthly);
    }
}
