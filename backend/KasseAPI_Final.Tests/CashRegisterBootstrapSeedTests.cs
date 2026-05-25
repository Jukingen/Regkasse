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
    private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SuspendedTenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegBootstrap_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedTenantsAsync(AppDbContext ctx)
    {
        ctx.Tenants.AddRange(
            new Tenant
            {
                Id = TenantAId,
                Name = "Tenant A",
                Slug = "tenant-a",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Tenant
            {
                Id = TenantBId,
                Name = "Tenant B",
                Slug = "tenant-b",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Tenant
            {
                Id = SuspendedTenantId,
                Name = "Tenant Suspended",
                Slug = "tenant-suspended",
                Status = TenantStatuses.Suspended,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task EnsureMinimal_WhenActiveTenantHasNoRegister_InsertsClosedDefaultRegisterPerTenant()
    {
        await using var ctx = CreateContext();
        await SeedTenantsAsync(ctx);
        await CashRegisterBootstrapSeed.EnsureMinimalOperationalCashRegisterWhenTableEmptyAsync(
            ctx,
            NullLogger.Instance);

        var registers = await ctx.CashRegisters.AsNoTracking()
            .OrderBy(r => r.TenantId)
            .ToListAsync();

        Assert.Equal(2, registers.Count);
        Assert.All(registers, r =>
        {
            Assert.Equal(RegisterStatus.Closed, r.Status);
            Assert.Equal("KASSE-001", r.RegisterNumber);
            Assert.Equal("Hauptkasse", r.Location);
            Assert.Null(r.CurrentUserId);
            Assert.True(r.IsActive);
        });
        Assert.DoesNotContain(registers, r => r.TenantId == SuspendedTenantId);
    }

    [Fact]
    public async Task EnsureMinimal_WhenTenantAlreadyHasRegister_SeedsOnlyMissingActiveTenants()
    {
        await using var ctx = CreateContext();
        await SeedTenantsAsync(ctx);
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = TenantAId,
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

        var registers = await ctx.CashRegisters.AsNoTracking()
            .OrderBy(r => r.TenantId)
            .ToListAsync();

        Assert.Equal(2, registers.Count);
        Assert.Contains(registers, r => r.TenantId == TenantAId && r.RegisterNumber == "X1");
        Assert.Contains(registers, r =>
            r.TenantId == TenantBId
            && r.RegisterNumber == "KASSE-001"
            && r.Status == RegisterStatus.Closed);
    }

    [Fact]
    public async Task EnsureMinimal_IsIdempotent_ForAlreadySeededActiveTenants()
    {
        await using var ctx = CreateContext();
        await SeedTenantsAsync(ctx);

        await CashRegisterBootstrapSeed.EnsureMinimalOperationalCashRegisterWhenTableEmptyAsync(
            ctx,
            NullLogger.Instance);
        await CashRegisterBootstrapSeed.EnsureMinimalOperationalCashRegisterWhenTableEmptyAsync(
            ctx,
            NullLogger.Instance);

        Assert.Equal(2, await ctx.CashRegisters.CountAsync());
    }
}
