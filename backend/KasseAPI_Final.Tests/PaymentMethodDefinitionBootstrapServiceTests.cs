using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentMethodDefinitionBootstrapServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"pmd_bootstrap_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    [Fact]
    public async Task EnsureDefaultsForCashRegisterAsync_SeedsStandardCatalog_WhenEmpty()
    {
        await using var ctx = CreateContext();
        var tenantId = LegacyDefaultTenantIds.Primary;
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "K-NEW",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var svc = new PaymentMethodDefinitionBootstrapService(ctx);
        await svc.EnsureDefaultsForCashRegisterAsync(tenantId, regId);

        var rows = await ctx.PaymentMethodDefinitions.Where(x => x.CashRegisterId == regId).ToListAsync();
        Assert.True(rows.Count >= 4);
        Assert.Contains(rows, x => x.Code == "cash" && x.IsDefault);
        Assert.Contains(rows, x => x.Code == "card");
    }

    [Fact]
    public async Task EnsureDefaultsForCashRegisterAsync_CopiesFromSiblingRegister_WhenTenantHasCatalog()
    {
        await using var ctx = CreateContext();
        var tenantId = LegacyDefaultTenantIds.Primary;
        var sourceReg = Guid.NewGuid();
        var targetReg = Guid.NewGuid();
        ctx.CashRegisters.AddRange(
            new CashRegister
            {
                Id = sourceReg,
                TenantId = tenantId,
                RegisterNumber = "K-01",
                Location = "A",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            },
            new CashRegister
            {
                Id = targetReg,
                TenantId = tenantId,
                RegisterNumber = "K-02",
                Location = "B",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            });
        var utc = DateTime.UtcNow;
        ctx.PaymentMethodDefinitions.Add(new PaymentMethodDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = sourceReg,
            Code = "cash",
            Name = "Custom Bar",
            IsActive = true,
            IsDefault = true,
            DisplayOrder = 1,
            LegacyPaymentMethodValue = 0,
            CreatedAtUtc = utc,
        });
        await ctx.SaveChangesAsync();

        var svc = new PaymentMethodDefinitionBootstrapService(ctx);
        await svc.EnsureDefaultsForCashRegisterAsync(tenantId, targetReg);

        var copied = await ctx.PaymentMethodDefinitions.SingleAsync(x => x.CashRegisterId == targetReg);
        Assert.Equal("cash", copied.Code);
        Assert.Equal("Custom Bar", copied.Name);
    }
}
