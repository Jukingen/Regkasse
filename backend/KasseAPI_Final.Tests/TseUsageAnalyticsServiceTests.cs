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

public sealed class TseUsageAnalyticsServiceTests
{
    [Fact]
    public async Task GetTseAnalytics_counts_registers_signatures_and_failures()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var enabledId = Guid.NewGuid();
        var disabledId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.CashRegisters.AddRange(
            new CashRegister
            {
                Id = enabledId,
                TenantId = tenantId,
                RegisterNumber = "K1",
                Location = "Wien",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Open,
                StartbelegCreatedAt = now.AddDays(-10),
                CreatedAt = now.AddDays(-10),
                IsActive = true,
            },
            new CashRegister
            {
                Id = disabledId,
                TenantId = tenantId,
                RegisterNumber = "K2",
                Location = "Graz",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Closed,
                CreatedAt = now.AddDays(-10),
                IsActive = true,
            });

        db.PaymentDetails.AddRange(
            Payment(enabledId, now.AddMinutes(-5), "header.payload.sig"),
            Payment(enabledId, now.AddMinutes(-2), "header.payload.sig"),
            Payment(disabledId, now.AddMinutes(-1), ""));
        await db.SaveChangesAsync();

        var sut = new TseUsageAnalyticsService(db, PassthroughCache(), NullLogger<TseUsageAnalyticsService>.Instance);
        var dto = await sut.GetTseAnalyticsAsync(now.AddDays(-7), now.AddMinutes(1));

        Assert.Equal(2, dto.TotalRegisters);
        Assert.Equal(2, dto.ActiveRegisters);
        Assert.Equal(1, dto.TseEnabled);
        Assert.Equal(1, dto.TseDisabled);
        Assert.Equal(2, dto.SignaturesToday);
        Assert.Equal(2, dto.SignaturesThisMonth);
        Assert.Equal(1, dto.FailedSignatures);
        Assert.True(dto.DiagnosticOnly);
        Assert.NotEmpty(dto.DailyUsage);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_usage_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
    }

    private static PaymentDetails Payment(Guid registerId, DateTime createdAt, string signature) =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "C",
            CashRegisterId = registerId,
            TotalAmount = 10m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            CashierId = "c1",
            TableNumber = 1,
            CreatedAt = createdAt,
            TseTimestamp = createdAt,
            TseSignature = signature,
            IsActive = true,
        };

    private static ICacheService PassthroughCache()
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<TseAnalyticsDto>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<TseAnalyticsDto>>, TimeSpan?, CancellationToken>(
                (_, factory, _, ct) => factory(ct));
        return cache.Object;
    }
}
