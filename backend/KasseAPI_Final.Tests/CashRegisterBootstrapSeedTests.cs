using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Tests;

public class CashRegisterBootstrapSeedTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegBootstrap_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task EnsureMinimal_WhenTableEmpty_InsertsOpenK01()
    {
        await using var ctx = CreateContext();
        await CashRegisterBootstrapSeed.EnsureMinimalOperationalCashRegisterWhenTableEmptyAsync(
            ctx,
            NullLogger.Instance);

        Assert.Equal(1, await ctx.CashRegisters.CountAsync());
        var r = await ctx.CashRegisters.AsNoTracking().SingleAsync();
        Assert.Equal(RegisterStatus.Open, r.Status);
        Assert.Equal("K01", r.RegisterNumber);
        Assert.Null(r.CurrentUserId);
        Assert.True(r.IsActive);
    }

    [Fact]
    public async Task EnsureMinimal_WhenAnyRowExists_DoesNotInsertSecond()
    {
        await using var ctx = CreateContext();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = Guid.NewGuid(),
            RegisterNumber = "X1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Disabled,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        await CashRegisterBootstrapSeed.EnsureMinimalOperationalCashRegisterWhenTableEmptyAsync(
            ctx,
            NullLogger.Instance);

        Assert.Equal(1, await ctx.CashRegisters.CountAsync());
    }
}
