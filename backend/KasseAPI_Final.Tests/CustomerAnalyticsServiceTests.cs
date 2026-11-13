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

        var sut = new CustomerAnalyticsService(db, PassthroughCache(), NullLogger<CustomerAnalyticsService>.Instance);
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
        Assert.Equal(100m, dto.Arpu);
        Assert.Equal(0m, dto.ChurnRate);
        Assert.Null(dto.CustomerLtv);
        Assert.Equal(1, dto.PlanDistribution.Trial);
        Assert.Equal(0, dto.PlanDistribution.Starter);
        Assert.Equal(1, dto.PlanDistribution.Business);
        Assert.Equal(0, dto.PlanDistribution.Plus);
    }

    [Fact]
    public async Task GetCustomerAnalytics_computes_monthly_churn_arpu_and_ltv()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(null);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetCustomerAnalytics_computes_monthly_churn_arpu_and_ltv))
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);

        var keptId = Guid.NewGuid();
        var lostId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        db.Tenants.AddRange(
            new Tenant
            {
                Id = keptId,
                Name = "Kept",
                Slug = "kept",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = now.AddMonths(-6),
            },
            new Tenant
            {
                Id = lostId,
                Name = "Lost",
                Slug = "lost",
                Status = TenantStatuses.Suspended,
                IsActive = false,
                CreatedAt = now.AddMonths(-6),
            });

        db.LicenseSales.AddRange(
            new LicenseSale
            {
                Id = Guid.NewGuid(),
                TenantId = keptId,
                LicenseKey = "REGK-KEEP",
                LicensePlan = LicenseSalePlans.TwelveMonths,
                LicenseType = LicenseType.Starter,
                ValidFromUtc = monthStart.AddMonths(-2),
                ValidUntilUtc = monthStart.AddMonths(10),
                PriceNet = 1200m,
                VatAmount = 240m,
                PriceGross = 1440m,
                Status = LicenseSaleStatuses.Active,
                SoldByUserId = Guid.NewGuid(),
                InvoiceNumber = "INV-KEEP",
            },
            new LicenseSale
            {
                Id = Guid.NewGuid(),
                TenantId = lostId,
                LicenseKey = "REGK-LOST",
                LicensePlan = LicenseSalePlans.TwelveMonths,
                LicenseType = LicenseType.Starter,
                ValidFromUtc = monthStart.AddMonths(-2),
                ValidUntilUtc = now > monthStart ? now : monthStart.AddMinutes(1),
                PriceNet = 1200m,
                VatAmount = 240m,
                PriceGross = 1440m,
                Status = LicenseSaleStatuses.Active,
                SoldByUserId = Guid.NewGuid(),
                InvoiceNumber = "INV-LOST",
            });
        await db.SaveChangesAsync();

        var sut = new CustomerAnalyticsService(db, PassthroughCache(), NullLogger<CustomerAnalyticsService>.Instance);
        var dto = await sut.GetCustomerAnalyticsAsync();

        Assert.Equal(1, dto.PaidTenants);
        Assert.Equal(50m, dto.ChurnRate);
        Assert.Equal(100m, dto.Arpu);
        Assert.Equal(200m, dto.CustomerLtv);
        Assert.Equal(1, dto.PlanDistribution.Starter);
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

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, 0, 0)]
    [InlineData(10, 1, 10)]
    [InlineData(4, 1, 25)]
    public void CalculateChurnRate_percent_of_start_cohort(int start, int lost, decimal expected)
    {
        Assert.Equal(expected, CustomerAnalyticsService.CalculateChurnRate(start, lost));
    }

    [Fact]
    public void CalculateArpu_divides_mrr_by_paid_tenants()
    {
        Assert.Equal(0m, CustomerAnalyticsService.CalculateArpu(100m, 0));
        Assert.Equal(50m, CustomerAnalyticsService.CalculateArpu(100m, 2));
    }

    [Fact]
    public void CalculateCustomerLtv_is_null_when_churn_is_zero()
    {
        Assert.Null(CustomerAnalyticsService.CalculateCustomerLtv(50m, 0m));
        Assert.Equal(500m, CustomerAnalyticsService.CalculateCustomerLtv(50m, 10m));
    }

    private static ICacheService PassthroughCache()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<CustomerAnalyticsDto>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<CustomerAnalyticsDto>>, TimeSpan?, CancellationToken>(
                (_, factory, _, ct) => factory(ct));
        return cache.Object;
    }
}
