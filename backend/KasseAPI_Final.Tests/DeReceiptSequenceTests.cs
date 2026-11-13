using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Countries.Strategies.Germany;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class DeReceiptSequenceTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public DeReceiptSequenceTests(PostgreSqlReplayFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void FormatDeBelegNr_IsSlugRegisterSequence()
    {
        Assert.Equal("DE-dev-1-1", DeReceiptSequenceService.FormatDeBelegNr("dev", "1", 1));
    }

    [SkippableFact]
    public async Task AllocateNextAsync_SameRegister_ReturnsOneThenTwo()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        var registerId = await SeedRegisterAsync(ctx, "1");
        var service = new DeReceiptSequenceService(ctx);

        var first = await service.AllocateNextAsync(SystemTenantIds.Platform, registerId);
        var second = await service.AllocateNextAsync(SystemTenantIds.Platform, registerId);

        Assert.Equal(1, first);
        Assert.Equal(2, second);
    }

    [SkippableFact]
    public async Task AllocateNextAsync_TwoRegisters_DoNotShareCounter()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        var a = await SeedRegisterAsync(ctx, "1");
        var b = await SeedRegisterAsync(ctx, "2");
        var service = new DeReceiptSequenceService(ctx);

        Assert.Equal(1, await service.AllocateNextAsync(SystemTenantIds.Platform, a));
        Assert.Equal(1, await service.AllocateNextAsync(SystemTenantIds.Platform, b));
        Assert.Equal(2, await service.AllocateNextAsync(SystemTenantIds.Platform, a));
    }

    [SkippableFact]
    public async Task AllocateNextAsync_ConcurrentSameRegister_ReturnsDistinctNumbers()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var seed = CreateContext();
        var registerId = await SeedRegisterAsync(seed, "9");

        await using var left = CreateContext();
        await using var right = CreateContext();
        var results = await Task.WhenAll(
            new DeReceiptSequenceService(left).AllocateNextAsync(SystemTenantIds.Platform, registerId),
            new DeReceiptSequenceService(right).AllocateNextAsync(SystemTenantIds.Platform, registerId));

        Assert.Equal(2, results.Distinct().Count());
        Assert.DoesNotContain(0, results);
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseAppNpgsql(_fixture.ConnectionString)
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static async Task<Guid> SeedRegisterAsync(AppDbContext ctx, string registerNumber)
    {
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        var registerId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = registerId,
            RegisterNumber = registerNumber + registerId.ToString("N")[..6],
            Location = "DE",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();
        return registerId;
    }
}
