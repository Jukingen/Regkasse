using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentTrendAnalysisServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"payment_trends_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    [Fact]
    public async Task GetTrendAnalysisAsync_groups_daily_and_excludes_storno()
    {
        await using var db = CreateContext();
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = TenantA,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        var day = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        db.PaymentDetails.AddRange(
            CreatePayment(regId, day, 100m, "0"),
            CreatePayment(regId, day.AddHours(2), 50m, "1"),
            CreatePayment(regId, day.AddDays(1), 200m, "0", isStorno: true));

        await db.SaveChangesAsync();

        var svc = new PaymentTrendAnalysisService(db);
        var result = await svc.GetTrendAnalysisAsync(
            TenantA,
            TrendPeriod.Daily,
            new DateTime(2026, 3, 15),
            new DateTime(2026, 3, 16),
            CancellationToken.None);

        Assert.Equal(TrendPeriod.Daily, result.Period);
        Assert.Equal(2, result.Summary.TotalTransactions);
        Assert.Equal(150m, result.Summary.TotalRevenue);
        Assert.Equal(2, result.TrendData.Count);
        Assert.Equal(150m, result.Comparison.CurrentPeriodTotal);
    }

    [Fact]
    public async Task GetTrendAnalysisAsync_tenant_isolation()
    {
        await using var db = CreateContext();
        var regA = Guid.NewGuid();
        var regB = Guid.NewGuid();
        db.CashRegisters.AddRange(
            new CashRegister
            {
                Id = regA,
                TenantId = TenantA,
                RegisterNumber = "A",
                Location = "L",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            },
            new CashRegister
            {
                Id = regB,
                TenantId = TenantB,
                RegisterNumber = "B",
                Location = "L",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });

        var day = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        db.PaymentDetails.AddRange(
            CreatePayment(regA, day, 100m, "0"),
            CreatePayment(regB, day, 500m, "0"));
        await db.SaveChangesAsync();

        var svc = new PaymentTrendAnalysisService(db);
        var result = await svc.GetTrendAnalysisAsync(
            TenantA,
            TrendPeriod.Daily,
            new DateTime(2026, 3, 15),
            new DateTime(2026, 3, 15),
            CancellationToken.None);

        Assert.Equal(100m, result.Summary.TotalRevenue);
    }

    private static PaymentDetails CreatePayment(
        Guid registerId,
        DateTime createdAt,
        decimal amount,
        string methodRaw,
        bool isStorno = false)
    {
        return new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "C",
            CashRegisterId = registerId,
            TotalAmount = amount,
            TaxAmount = 0m,
            PaymentMethodRaw = methodRaw,
            CashierId = "cashier",
            TableNumber = 1,
            CreatedAt = createdAt,
            IsActive = true,
            IsStorno = isStorno,
        };
    }
}
