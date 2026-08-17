using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Analytics;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentVolumeAnalyticsServiceTests
{
    [Fact]
    public async Task GetPaymentVolume_sums_range_and_monthly_growth()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var thisMonth = new DateTime(now.Year, now.Month, 1, 12, 0, 0, DateTimeKind.Utc);
        var lastMonth = thisMonth.AddMonths(-1);

        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Wien",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Open,
            CreatedAt = lastMonth,
            IsActive = true,
        });

        db.PaymentDetails.AddRange(
            Payment(registerId, lastMonth.AddDays(2), 100m),
            Payment(registerId, now.AddMinutes(-5), 250m),
            Payment(registerId, now.AddMinutes(-2), 50m, isStorno: true));
        await db.SaveChangesAsync();

        var sut = new PaymentVolumeAnalyticsService(
            db,
            PassthroughCache(),
            NullLogger<PaymentVolumeAnalyticsService>.Instance);
        var dto = await sut.GetPaymentVolumeAnalyticsAsync(lastMonth, now.AddMinutes(1), "month");

        Assert.Equal(350m, dto.TotalRevenue);
        Assert.Equal(2, dto.TotalTransactions);
        Assert.Equal(250m, dto.RevenueThisMonth);
        Assert.Equal(100m, dto.RevenueLastMonth);
        Assert.Equal(150m, dto.MonthlyGrowth);
        Assert.Equal(1, dto.TransactionsThisMonth);
        Assert.Equal(1, dto.TransactionsLastMonth);
        Assert.Equal(175m, dto.AverageTransactionValue);
        Assert.NotEmpty(dto.DailyVolume);
        Assert.NotEmpty(dto.MonthlyVolume);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(50, 0, 100)]
    [InlineData(150, 100, 50)]
    [InlineData(50, 100, -50)]
    public void CalculateMonthlyGrowth_handles_zero_baseline(decimal current, decimal previous, decimal expected)
    {
        Assert.Equal(expected, PaymentVolumeAnalyticsService.CalculateMonthlyGrowth(current, previous));
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"pay_vol_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
    }

    private static PaymentDetails Payment(Guid registerId, DateTime createdAt, decimal amount, bool isStorno = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "C",
            CashRegisterId = registerId,
            TotalAmount = amount,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            CashierId = "c1",
            TableNumber = 1,
            CreatedAt = createdAt,
            IsActive = true,
            IsStorno = isStorno,
        };

    private static ICacheService PassthroughCache()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<PaymentVolumeAnalyticsDto>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<PaymentVolumeAnalyticsDto>>, TimeSpan?, CancellationToken>(
                (_, factory, _, ct) => factory(ct));
        return cache.Object;
    }
}
