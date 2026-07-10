using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TagesabschlussServiceOperationalTests
{
    private static AppDbContext CreateContext(Guid tenantId)
    {
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"TagesabschlussOp_{Guid.NewGuid():N}")
                .Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static TagesabschlussService CreateService(AppDbContext ctx) =>
        new(
            ctx,
            Mock.Of<ITseService>(),
            new FakeTseProvider(NullLogger<FakeTseProvider>.Instance),
            new SoftwareTseKeyProvider(),
            Mock.Of<IFinanzOnlineService>(),
            Options.Create(new TseOptions()),
            Mock.Of<IHostEnvironment>(),
            NullLogger<TagesabschlussService>.Instance);

    [Fact]
    public async Task ResolveOperationalCashRegisterIdAsync_WhenNull_PicksDefaultThenFirst()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var defaultId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-op", IsActive = true });
        ctx.CashRegisters.AddRange(
            new CashRegister
            {
                Id = otherId,
                TenantId = tenantId,
                RegisterNumber = "K2",
                Location = "Bar",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsDefaultForTenant = false,
            },
            new CashRegister
            {
                Id = defaultId,
                TenantId = tenantId,
                RegisterNumber = "K1",
                Location = "Haupt",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsDefaultForTenant = true,
            });
        await ctx.SaveChangesAsync();

        var sut = CreateService(ctx);
        var resolved = await sut.ResolveOperationalCashRegisterIdAsync(tenantId, null);

        Assert.Equal(defaultId, resolved);
    }

    [Fact]
    public async Task GetClosingHistoryAsync_ReturnsRegisterScopedRows_NotFilteredByActorUser()
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var closingDay = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(
            PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 5, 1));

        await using var ctx = CreateContext(tenantId);
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-hist", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
        });
        ctx.DailyClosings.Add(new DailyClosing
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = registerId,
            UserId = "manager-user",
            ClosingDate = closingDay,
            ClosingType = "Daily",
            TotalAmount = 120m,
            TotalTaxAmount = 20m,
            TransactionCount = 3,
            TseSignature = "sig",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var sut = CreateService(ctx);
        var history = await sut.GetClosingHistoryAsync(
            new DateTime(2026, 5, 1),
            new DateTime(2026, 5, 1),
            registerId);

        Assert.Single(history);
        Assert.Equal(120m, history[0].TotalAmount);
        Assert.Equal("manager-user", await ctx.DailyClosings.Select(d => d.UserId).FirstAsync());
    }
}
