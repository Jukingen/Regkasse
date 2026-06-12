using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Payment method catalog is scoped per cash register; tenant isolation and register resolution remain enforced.
/// </summary>
public sealed class Wave2TenantScopedPaymentMethodsAndCashRegistersTests
{
    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Wave2_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static readonly Guid TenantA = LegacyDefaultTenantIds.Primary;
    private static readonly Guid TenantB = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static CashRegister SeedRegister(AppDbContext ctx, Guid tenantId, Guid regId, string number)
    {
        var reg = new CashRegister
        {
            TenantId = tenantId,
            Id = regId,
            RegisterNumber = number,
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            CurrentUserId = "u1",
        };
        ctx.CashRegisters.Add(reg);
        return reg;
    }

    [Fact]
    public async Task PaymentMethodCatalog_SameCode_TwoRegistersSameTenant_ResolverSelectsPerRegister()
    {
        await using var ctx = CreateContext(TenantA);
        var regA = Guid.NewGuid();
        var regB = Guid.NewGuid();
        SeedRegister(ctx, TenantA, regA, "K-01");
        SeedRegister(ctx, TenantA, regB, "K-02");
        var now = DateTime.UtcNow;
        ctx.PaymentMethodDefinitions.Add(new PaymentMethodDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            CashRegisterId = regA,
            Code = "cash",
            Name = "Bar K1",
            IsActive = true,
            IsDefault = false,
            DisplayOrder = 0,
            LegacyPaymentMethodValue = 0,
            CreatedAtUtc = now,
        });
        ctx.PaymentMethodDefinitions.Add(new PaymentMethodDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            CashRegisterId = regB,
            Code = "cash",
            Name = "Bar K2",
            IsActive = true,
            IsDefault = false,
            DisplayOrder = 0,
            LegacyPaymentMethodValue = 0,
            CreatedAtUtc = now,
        });
        await ctx.SaveChangesAsync();

        var svc = new PaymentMethodCatalogService(ctx, TenantTestDoubles.SettingsResolverReturning(TenantA));

        var listA = await svc.GetActivePosMethodsAsync(regA);
        var listB = await svc.GetActivePosMethodsAsync(regB);
        Assert.Single(listA);
        Assert.Single(listB);
        Assert.Equal("Bar K1", listA[0].Name);
        Assert.Equal("Bar K2", listB[0].Name);

        var resA = await svc.ResolveForPaymentAsync("cash", regA);
        var resB = await svc.ResolveForPaymentAsync("cash", regB);
        Assert.True(resA.Ok);
        Assert.True(resB.Ok);
        Assert.Equal("0", resA.LegacyRaw);
        Assert.Equal("0", resB.LegacyRaw);
    }

    [Fact]
    public async Task PaymentMethodCatalog_TwoTenants_IsolatedByRegisterAndTenant()
    {
        await using var ctx = CreateContext(TenantA);
        var regA = Guid.NewGuid();
        var regB = Guid.NewGuid();
        SeedRegister(ctx, TenantA, regA, "K-01");
        SeedRegister(ctx, TenantB, regB, "K-01");
        var now = DateTime.UtcNow;
        ctx.PaymentMethodDefinitions.Add(new PaymentMethodDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            CashRegisterId = regA,
            Code = "cash",
            Name = "Bar A",
            IsActive = true,
            IsDefault = false,
            DisplayOrder = 0,
            LegacyPaymentMethodValue = 0,
            CreatedAtUtc = now,
        });
        ctx.PaymentMethodDefinitions.Add(new PaymentMethodDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = TenantB,
            CashRegisterId = regB,
            Code = "cash",
            Name = "Bar B",
            IsActive = true,
            IsDefault = false,
            DisplayOrder = 0,
            LegacyPaymentMethodValue = 0,
            CreatedAtUtc = now,
        });
        await ctx.SaveChangesAsync();

        var svcA = new PaymentMethodCatalogService(ctx, TenantTestDoubles.SettingsResolverReturning(TenantA));
        var svcB = new PaymentMethodCatalogService(ctx, TenantTestDoubles.SettingsResolverReturning(TenantB));

        var listA = await svcA.GetActivePosMethodsAsync(regA);
        var listB = await svcB.GetActivePosMethodsAsync(regB);
        Assert.Equal("Bar A", listA[0].Name);
        Assert.Equal("Bar B", listB[0].Name);
    }

    [Fact]
    public async Task CashRegisterResolution_ListSelectable_ExcludesOtherTenantRegisters()
    {
        await using var ctx = CreateContext(TenantA);
        var regA = Guid.NewGuid();
        var regB = Guid.NewGuid();
        SeedRegister(ctx, TenantA, regA, "K-01");
        SeedRegister(ctx, TenantB, regB, "K-01");
        await ctx.SaveChangesAsync();

        var resolverA = new CashRegisterResolutionService(
            ctx,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CashRegisterResolutionService>.Instance,
            TenantTestDoubles.SettingsResolverReturning(TenantA),
            RksvStartbelegTestDoubles.GateOff(), RksvMonatsbelegTestDoubles.GateOff());

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, Roles.Cashier) }, "test"));
        var picker = await resolverA.ListSelectableForPosPickerAsync("u1", principal, default);
        Assert.Single(picker.Registers);
        Assert.Equal(regA, picker.Registers[0].Id);
    }

    [Fact]
    public async Task LegacySingleTenant_Data_WithPrimaryTenant_RemainsVisible()
    {
        await using var ctx = CreateContext(TenantA);
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = TenantA,
            Id = regId,
            RegisterNumber = "LEGACY-1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            CurrentUserId = "cashier",
        });
        await ctx.SaveChangesAsync();

        var resolver = new CashRegisterResolutionService(
            ctx,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CashRegisterResolutionService>.Instance, TenantTestDoubles.PrimaryTenantResolver, RksvStartbelegTestDoubles.GateOff(), RksvMonatsbelegTestDoubles.GateOff());

        var gate = await resolver.ValidatePaymentRegisterAsync("cashier", regId, new ClaimsPrincipal(), default);
        Assert.True(gate.Ok);
        Assert.Equal(regId, gate.ResolvedRegisterId);
    }
}
