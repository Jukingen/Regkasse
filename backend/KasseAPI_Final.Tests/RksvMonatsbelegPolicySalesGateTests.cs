using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvMonatsbelegPolicySalesGateTests
{
    private sealed class FixedUtcTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MonPolGate_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static async Task<Guid> SeedAsync(AppDbContext ctx, string blockingMode)
    {
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CompanySettings.Add(new CompanySettings
        {
            TenantId = SystemTenantIds.Platform,
            CompanyName = "Test GmbH",
            CompanyAddress = "Wien",
            CompanyTaxNumber = "ATU12345678",
            BusinessHours = new Dictionary<string, string>(),
            MonatsbelegBlockingMode = blockingMode,
        });
        await ctx.SaveChangesAsync();
        return regId;
    }

    private static RksvMonatsbelegPolicy CreatePolicy(AppDbContext ctx, DateTime utcNow) =>
        new(
            ctx,
            Options.Create(new TseOptions { TseMode = "Device", Mode = "Real" }),
            new FixedUtcTimeProvider(utcNow));

    [Fact]
    public async Task Missing_GracePeriod_Day10_AllowsWithYellowWarning()
    {
        await using var ctx = CreateContext();
        var regId = await SeedAsync(ctx, MonatsbelegBlockingModeNames.GracePeriod);
        var pol = CreatePolicy(ctx, new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc));

        var d = await pol.EvaluateSalesGateAsync(regId, CancellationToken.None);

        Assert.True(d.PreviousMonthMissing);
        Assert.False(d.BlocksSales);
        Assert.True(d.CanContinueWithWarning);
        Assert.Equal(MonatsbelegSalesGateEvaluator.WarningYellow, d.WarningLevel);
        Assert.Equal(MonatsbelegBlockingMode.GracePeriod, d.Mode);
    }

    [Fact]
    public async Task Missing_GracePeriod_Day15_Blocks()
    {
        await using var ctx = CreateContext();
        var regId = await SeedAsync(ctx, MonatsbelegBlockingModeNames.GracePeriod);
        var pol = CreatePolicy(ctx, new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc));

        var d = await pol.EvaluateSalesGateAsync(regId, CancellationToken.None);

        Assert.True(d.BlocksSales);
        Assert.False(d.CanContinueWithWarning);
        Assert.Equal(MonatsbelegSalesGateEvaluator.WarningRed, d.WarningLevel);
    }

    [Fact]
    public async Task Missing_Strict_AlwaysBlocks()
    {
        await using var ctx = CreateContext();
        var regId = await SeedAsync(ctx, MonatsbelegBlockingModeNames.Strict);
        var pol = CreatePolicy(ctx, new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc));

        var d = await pol.EvaluateSalesGateAsync(regId, CancellationToken.None);

        Assert.True(d.BlocksSales);
        Assert.Equal(MonatsbelegBlockingMode.Strict, d.Mode);
    }

    [Fact]
    public async Task Missing_WarningOnly_NeverBlocks()
    {
        await using var ctx = CreateContext();
        var regId = await SeedAsync(ctx, MonatsbelegBlockingModeNames.WarningOnly);
        var pol = CreatePolicy(ctx, new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc));

        var d = await pol.EvaluateSalesGateAsync(regId, CancellationToken.None);

        Assert.False(d.BlocksSales);
        Assert.True(d.CanContinueWithWarning);
        Assert.Equal(MonatsbelegSalesGateEvaluator.WarningRed, d.WarningLevel);
    }
}
