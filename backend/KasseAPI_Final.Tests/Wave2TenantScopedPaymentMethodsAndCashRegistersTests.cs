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
/// Wave 2: <see cref="PaymentMethodDefinition"/> and <see cref="CashRegister"/> are scoped by tenant; catalog and resolver honor <see cref="ISettingsTenantResolver"/>.
/// </summary>
public sealed class Wave2TenantScopedPaymentMethodsAndCashRegistersTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Wave2_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static readonly Guid TenantA = LegacyDefaultTenantIds.Primary;
    private static readonly Guid TenantB = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task PaymentMethodCatalog_SameCode_TwoTenants_BothActive_ResolverSelectsCorrectRow()
    {
        await using var ctx = CreateContext();
        var now = DateTime.UtcNow;
        ctx.PaymentMethodDefinitions.Add(new PaymentMethodDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
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

        var resA = await svcA.ResolveForPaymentAsync("cash");
        var resB = await svcB.ResolveForPaymentAsync("cash");

        Assert.True(resA.Ok);
        Assert.True(resB.Ok);
        Assert.Equal("0", resA.LegacyRaw);
        Assert.Equal("0", resB.LegacyRaw);

        var listA = await svcA.GetActivePosMethodsAsync();
        var listB = await svcB.GetActivePosMethodsAsync();
        Assert.Single(listA);
        Assert.Single(listB);
        Assert.Equal("Bar A", listA[0].Name);
        Assert.Equal("Bar B", listB[0].Name);
    }

    [Fact]
    public async Task CashRegisterResolution_ListSelectable_ExcludesOtherTenantRegisters()
    {
        await using var ctx = CreateContext();
        var regA = Guid.NewGuid();
        var regB = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = TenantA,
            Id = regA,
            RegisterNumber = "K-01",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            CurrentUserId = "u1",
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = TenantB,
            Id = regB,
            RegisterNumber = "K-01",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            CurrentUserId = "u2",
        });
        await ctx.SaveChangesAsync();

        var resolverA = new CashRegisterResolutionService(
            ctx,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CashRegisterResolutionService>.Instance,
            TenantTestDoubles.SettingsResolverReturning(TenantA));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, Roles.Cashier) }, "test"));
        var picker = await resolverA.ListSelectableForPosPickerAsync("u1", principal, default);
        Assert.Single(picker.Registers);
        Assert.Equal(regA, picker.Registers[0].Id);
    }

    [Fact]
    public async Task LegacySingleTenant_Data_WithPrimaryTenant_RemainsVisible()
    {
        await using var ctx = CreateContext();
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
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CashRegisterResolutionService>.Instance,
            TenantTestDoubles.PrimaryTenantResolver);

        var gate = await resolver.ValidatePaymentRegisterAsync("cashier", regId, new ClaimsPrincipal(), default);
        Assert.True(gate.Ok);
        Assert.Equal(regId, gate.ResolvedRegisterId);
    }
}
