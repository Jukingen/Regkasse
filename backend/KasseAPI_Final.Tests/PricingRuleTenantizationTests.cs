using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Pricing rules: admin tenant gate and resolver tenant-bound candidates (join on product/category tenant).
/// </summary>
public sealed class PricingRuleTenantizationTests
{
    private static readonly Guid TenantA = LegacyDefaultTenantIds.Primary;
    private static readonly Guid TenantB = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PricingTenant_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static void EnsureTenants(AppDbContext ctx)
    {
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        if (!ctx.Tenants.AsNoTracking().Any(t => t.Id == TenantB))
            ctx.Tenants.Add(new Tenant { Id = TenantB, Name = "Tenant B", Slug = "pricing-tenant-b" });
    }

    private static AdminPricingRulesController CreateAdminController(AppDbContext ctx, Guid effectiveTenantId) =>
        new(
            ctx,
            NullLogger<AdminPricingRulesController>.Instance,
            TenantTestDoubles.SettingsResolverReturning(effectiveTenantId));

    private static async Task<(Guid catA, Guid prodA, Guid catB, Guid prodB, Guid ruleB)> SeedTwoTenantsWithRuleOnBAsync(AppDbContext ctx)
    {
        EnsureTenants(ctx);
        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = TenantA, Id = catA, Name = "CA", VatRate = 10m });
        ctx.Categories.Add(new Category { TenantId = TenantB, Id = catB, Name = "CB", VatRate = 10m });
        var prodA = Guid.NewGuid();
        var prodB = Guid.NewGuid();
        ctx.Products.Add(new Product
        {
            Id = prodA,
            TenantId = TenantA,
            Name = "PA",
            Price = 10m,
            CategoryId = catA,
            Category = "CA",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = "bc-pr-a",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        ctx.Products.Add(new Product
        {
            Id = prodB,
            TenantId = TenantB,
            Name = "PB",
            Price = 20m,
            CategoryId = catB,
            Category = "CB",
            StockQuantity = 1,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Reduced,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Barcode = "bc-pr-b",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ruleB = Guid.NewGuid();
        ctx.PricingRules.Add(new PricingRule
        {
            Id = ruleB,
            Name = "B rule",
            Priority = 100,
            IsActive = true,
            ValidFromDate = today.AddDays(-1),
            ValidToDate = today.AddDays(30),
            DaysOfWeekMask = 0b1111111,
            TimeWindowEnabled = false,
            TimeStartMinutes = 0,
            TimeEndMinutes = 1439,
            TargetScope = PricingRuleTargetScope.Product,
            TargetId = prodB,
            ActionType = PricingRuleActionType.FixedGrossPrice,
            ActionValue = 1.11m,
            CashRegisterId = null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        return (catA, prodA, catB, prodB, ruleB);
    }

    private static CashRegister NewCashRegister(Guid tenantId, Guid id, string registerNumber) => new()
    {
        Id = id,
        TenantId = tenantId,
        RegisterNumber = registerNumber,
        Location = "Test",
        StartingBalance = 0,
        CurrentBalance = 0,
        LastBalanceUpdate = DateTime.UtcNow,
        Status = RegisterStatus.Open,
        CreatedAt = DateTime.UtcNow,
        IsActive = true
    };

    private static CreatePricingRuleRequest BaseRequest(
        DateOnly today,
        PricingRuleTargetScope scope,
        Guid targetId,
        Guid? cashRegisterId = null) => new()
        {
            Name = "rule",
            Priority = 50,
            ValidFromDate = today,
            ValidToDate = today.AddDays(7),
            TargetScope = scope,
            TargetId = targetId,
            ActionType = PricingRuleActionType.FixedGrossPrice,
            ActionValue = 5.55m,
            CashRegisterId = cashRegisterId
        };

    [Fact]
    public async Task Admin_GetAll_AsTenantA_ExcludesTenantBRule()
    {
        await using var ctx = CreateContext();
        var (_, _, _, _, ruleB) = await SeedTwoTenantsWithRuleOnBAsync(ctx);

        var c = CreateAdminController(ctx, TenantA);
        var result = await c.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<PricingRuleAdminDto>>(ok.Value);
        Assert.DoesNotContain(list, x => x.Id == ruleB);
    }

    [Fact]
    public async Task Admin_GetById_OtherTenantRule_Returns404()
    {
        await using var ctx = CreateContext();
        var (_, _, _, _, ruleB) = await SeedTwoTenantsWithRuleOnBAsync(ctx);

        var c = CreateAdminController(ctx, TenantA);
        var result = await c.GetById(ruleB, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Admin_Create_TargetProductOtherTenant_Returns400()
    {
        await using var ctx = CreateContext();
        var (_, _, _, prodB, _) = await SeedTwoTenantsWithRuleOnBAsync(ctx);
        ctx.PricingRules.RemoveRange(ctx.PricingRules);
        await ctx.SaveChangesAsync();

        var c = CreateAdminController(ctx, TenantA);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await c.Create(
            new CreatePricingRuleRequest
            {
                Name = "bad",
                Priority = 1,
                ValidFromDate = today,
                ValidToDate = today.AddDays(1),
                TargetScope = PricingRuleTargetScope.Product,
                TargetId = prodB,
                ActionType = PricingRuleActionType.PercentOffList,
                ActionValue = 5m
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Admin_Update_OtherTenantRule_Returns404()
    {
        await using var ctx = CreateContext();
        var (catA, prodA, _, _, ruleB) = await SeedTwoTenantsWithRuleOnBAsync(ctx);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var c = CreateAdminController(ctx, TenantA);
        var result = await c.Update(
            ruleB,
            new UpdatePricingRuleRequest
            {
                Name = "hijack",
                Priority = 999,
                ValidFromDate = today,
                ValidToDate = today.AddDays(1),
                TargetScope = PricingRuleTargetScope.Product,
                TargetId = prodA,
                ActionType = PricingRuleActionType.PercentOffList,
                ActionValue = 1m
            },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        var stillB = await ctx.PricingRules.AsNoTracking().SingleAsync(r => r.Id == ruleB);
        Assert.Equal("B rule", stillB.Name);
    }

    [Fact]
    public async Task Admin_Delete_OtherTenantRule_Returns404()
    {
        await using var ctx = CreateContext();
        var (_, _, _, _, ruleB) = await SeedTwoTenantsWithRuleOnBAsync(ctx);

        var c = CreateAdminController(ctx, TenantA);
        var result = await c.Delete(ruleB, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.True(await ctx.PricingRules.AsNoTracking().Where(r => r.Id == ruleB).Select(r => r.IsActive).SingleAsync());
    }

    [Fact]
    public async Task Admin_Create_TargetCategoryOtherTenant_Returns400()
    {
        await using var ctx = CreateContext();
        var (_, _, catB, _, _) = await SeedTwoTenantsWithRuleOnBAsync(ctx);
        ctx.PricingRules.RemoveRange(ctx.PricingRules);
        await ctx.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var c = CreateAdminController(ctx, TenantA);
        var result = await c.Create(
            BaseRequest(today, PricingRuleTargetScope.Category, catB),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Admin_Update_OwnRule_WithOtherTenantProductTarget_Returns400()
    {
        await using var ctx = CreateContext();
        var (catA, prodA, _, prodB, _) = await SeedTwoTenantsWithRuleOnBAsync(ctx);
        ctx.PricingRules.RemoveRange(ctx.PricingRules);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ruleA = Guid.NewGuid();
        ctx.PricingRules.Add(new PricingRule
        {
            Id = ruleA,
            Name = "A rule",
            Priority = 10,
            IsActive = true,
            ValidFromDate = today.AddDays(-1),
            ValidToDate = today.AddDays(30),
            DaysOfWeekMask = 0b1111111,
            TimeWindowEnabled = false,
            TimeStartMinutes = 0,
            TimeEndMinutes = 1439,
            TargetScope = PricingRuleTargetScope.Product,
            TargetId = prodA,
            ActionType = PricingRuleActionType.FixedGrossPrice,
            ActionValue = 8m,
            CashRegisterId = null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var c = CreateAdminController(ctx, TenantA);
        var result = await c.Update(
            ruleA,
            new UpdatePricingRuleRequest
            {
                Name = "A rule",
                Priority = 10,
                ValidFromDate = today,
                ValidToDate = today.AddDays(7),
                TargetScope = PricingRuleTargetScope.Product,
                TargetId = prodB,
                ActionType = PricingRuleActionType.FixedGrossPrice,
                ActionValue = 8m
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        var row = await ctx.PricingRules.AsNoTracking().SingleAsync(r => r.Id == ruleA);
        Assert.Equal(prodA, row.TargetId);
    }

    [Fact]
    public async Task Admin_Update_OwnRule_WithOtherTenantCategoryTarget_Returns400()
    {
        await using var ctx = CreateContext();
        var (catA, prodA, catB, _, _) = await SeedTwoTenantsWithRuleOnBAsync(ctx);
        ctx.PricingRules.RemoveRange(ctx.PricingRules);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ruleA = Guid.NewGuid();
        ctx.PricingRules.Add(new PricingRule
        {
            Id = ruleA,
            Name = "Cat rule A",
            Priority = 5,
            IsActive = true,
            ValidFromDate = today.AddDays(-1),
            ValidToDate = today.AddDays(30),
            DaysOfWeekMask = 0b1111111,
            TimeWindowEnabled = false,
            TimeStartMinutes = 0,
            TimeEndMinutes = 1439,
            TargetScope = PricingRuleTargetScope.Category,
            TargetId = catA,
            ActionType = PricingRuleActionType.PercentOffList,
            ActionValue = 5m,
            CashRegisterId = null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var c = CreateAdminController(ctx, TenantA);
        var result = await c.Update(
            ruleA,
            new UpdatePricingRuleRequest
            {
                Name = "Cat rule A",
                Priority = 5,
                ValidFromDate = today,
                ValidToDate = today.AddDays(7),
                TargetScope = PricingRuleTargetScope.Category,
                TargetId = catB,
                ActionType = PricingRuleActionType.PercentOffList,
                ActionValue = 5m
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(catA, (await ctx.PricingRules.AsNoTracking().SingleAsync(r => r.Id == ruleA)).TargetId);
    }

    [Fact]
    public async Task Admin_Create_CashRegisterOtherTenant_Returns400()
    {
        await using var ctx = CreateContext();
        var (catA, prodA, _, _, _) = await SeedTwoTenantsWithRuleOnBAsync(ctx);
        ctx.PricingRules.RemoveRange(ctx.PricingRules);
        var regA = Guid.NewGuid();
        var regB = Guid.NewGuid();
        ctx.CashRegisters.Add(NewCashRegister(TenantA, regA, "REG-A"));
        ctx.CashRegisters.Add(NewCashRegister(TenantB, regB, "REG-B"));
        await ctx.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var c = CreateAdminController(ctx, TenantA);
        var req = BaseRequest(today, PricingRuleTargetScope.Product, prodA, regB);
        var result = await c.Create(req, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Admin_Update_CashRegisterOtherTenant_Returns400()
    {
        await using var ctx = CreateContext();
        var (catA, prodA, _, _, _) = await SeedTwoTenantsWithRuleOnBAsync(ctx);
        ctx.PricingRules.RemoveRange(ctx.PricingRules);
        var regA = Guid.NewGuid();
        var regB = Guid.NewGuid();
        ctx.CashRegisters.Add(NewCashRegister(TenantA, regA, "REG-A"));
        ctx.CashRegisters.Add(NewCashRegister(TenantB, regB, "REG-B"));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ruleA = Guid.NewGuid();
        ctx.PricingRules.Add(new PricingRule
        {
            Id = ruleA,
            Name = "A reg rule",
            Priority = 3,
            IsActive = true,
            ValidFromDate = today.AddDays(-1),
            ValidToDate = today.AddDays(30),
            DaysOfWeekMask = 0b1111111,
            TimeWindowEnabled = false,
            TimeStartMinutes = 0,
            TimeEndMinutes = 1439,
            TargetScope = PricingRuleTargetScope.Product,
            TargetId = prodA,
            ActionType = PricingRuleActionType.FixedGrossPrice,
            ActionValue = 9m,
            CashRegisterId = regA,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var c = CreateAdminController(ctx, TenantA);
        var result = await c.Update(
            ruleA,
            new UpdatePricingRuleRequest
            {
                Name = "A reg rule",
                Priority = 3,
                ValidFromDate = today,
                ValidToDate = today.AddDays(7),
                TargetScope = PricingRuleTargetScope.Product,
                TargetId = prodA,
                ActionType = PricingRuleActionType.FixedGrossPrice,
                ActionValue = 9m,
                CashRegisterId = regB
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(regA, (await ctx.PricingRules.AsNoTracking().SingleAsync(r => r.Id == ruleA)).CashRegisterId);
    }

    [Fact]
    public async Task Resolver_TenantA_AppliesOwnTenantRule()
    {
        await using var ctx = CreateContext();
        var (catA, prodA, _, _, _) = await SeedTwoTenantsWithRuleOnBAsync(ctx);
        ctx.PricingRules.RemoveRange(ctx.PricingRules);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        ctx.PricingRules.Add(new PricingRule
        {
            Id = Guid.NewGuid(),
            Name = "A happy hour",
            Priority = 200,
            IsActive = true,
            ValidFromDate = today.AddDays(-1),
            ValidToDate = today.AddDays(30),
            DaysOfWeekMask = 0b1111111,
            TimeWindowEnabled = false,
            TimeStartMinutes = 0,
            TimeEndMinutes = 1439,
            TargetScope = PricingRuleTargetScope.Product,
            TargetId = prodA,
            ActionType = PricingRuleActionType.FixedGrossPrice,
            ActionValue = 7.77m,
            CashRegisterId = null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var resolverA = new PricingRuleResolver(ctx, TenantTestDoubles.SettingsResolverReturning(TenantA));
        var res = await resolverA.ResolveUnitGrossAsync(10m, prodA, catA, null, DateTime.UtcNow);

        Assert.Equal(7.77m, res.UnitPriceGross);
        Assert.NotNull(res.AppliedRuleId);
    }

    [Fact]
    public async Task Resolver_OtherTenantCategoryRule_NotAppliedToTenantAProduct()
    {
        await using var ctx = CreateContext();
        var (catA, prodA, catB, _, _) = await SeedTwoTenantsWithRuleOnBAsync(ctx);
        ctx.PricingRules.RemoveRange(ctx.PricingRules);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        ctx.PricingRules.Add(new PricingRule
        {
            Id = Guid.NewGuid(),
            Name = "B category discount",
            Priority = 500,
            IsActive = true,
            ValidFromDate = today.AddDays(-1),
            ValidToDate = today.AddDays(30),
            DaysOfWeekMask = 0b1111111,
            TimeWindowEnabled = false,
            TimeStartMinutes = 0,
            TimeEndMinutes = 1439,
            TargetScope = PricingRuleTargetScope.Category,
            TargetId = catB,
            ActionType = PricingRuleActionType.PercentOffList,
            ActionValue = 50m,
            CashRegisterId = null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var resolverA = new PricingRuleResolver(ctx, TenantTestDoubles.SettingsResolverReturning(TenantA));
        var res = await resolverA.ResolveUnitGrossAsync(10m, prodA, catA, null, DateTime.UtcNow);

        Assert.Equal(10m, res.UnitPriceGross);
        Assert.Null(res.AppliedRuleId);
    }

    [Fact]
    public async Task Resolver_HigherPriorityRuleWins_RegressionGuard()
    {
        await using var ctx = CreateContext();
        var (catA, prodA, _, _, _) = await SeedTwoTenantsWithRuleOnBAsync(ctx);
        ctx.PricingRules.RemoveRange(ctx.PricingRules);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        ctx.PricingRules.Add(new PricingRule
        {
            Id = Guid.NewGuid(),
            Name = "low",
            Priority = 10,
            IsActive = true,
            ValidFromDate = today.AddDays(-1),
            ValidToDate = today.AddDays(30),
            DaysOfWeekMask = 0b1111111,
            TimeWindowEnabled = false,
            TimeStartMinutes = 0,
            TimeEndMinutes = 1439,
            TargetScope = PricingRuleTargetScope.Product,
            TargetId = prodA,
            ActionType = PricingRuleActionType.FixedGrossPrice,
            ActionValue = 6m,
            CashRegisterId = null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        ctx.PricingRules.Add(new PricingRule
        {
            Id = Guid.NewGuid(),
            Name = "high",
            Priority = 100,
            IsActive = true,
            ValidFromDate = today.AddDays(-1),
            ValidToDate = today.AddDays(30),
            DaysOfWeekMask = 0b1111111,
            TimeWindowEnabled = false,
            TimeStartMinutes = 0,
            TimeEndMinutes = 1439,
            TargetScope = PricingRuleTargetScope.Product,
            TargetId = prodA,
            ActionType = PricingRuleActionType.FixedGrossPrice,
            ActionValue = 4m,
            CashRegisterId = null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var resolverA = new PricingRuleResolver(ctx, TenantTestDoubles.SettingsResolverReturning(TenantA));
        var res = await resolverA.ResolveUnitGrossAsync(10m, prodA, catA, null, DateTime.UtcNow);

        Assert.Equal(4m, res.UnitPriceGross);
    }

    [Fact]
    public async Task Resolver_IgnoresOtherTenantProductRule()
    {
        await using var ctx = CreateContext();
        var (catA, prodA, catB, prodB, _) = await SeedTwoTenantsWithRuleOnBAsync(ctx);

        var resolverA = new PricingRuleResolver(ctx, TenantTestDoubles.SettingsResolverReturning(TenantA));
        var res = await resolverA.ResolveUnitGrossAsync(10m, prodA, catA, null, DateTime.UtcNow);

        Assert.Equal(10m, res.UnitPriceGross);
        Assert.Null(res.AppliedRuleId);

        var resolverB = new PricingRuleResolver(ctx, TenantTestDoubles.SettingsResolverReturning(TenantB));
        var resB = await resolverB.ResolveUnitGrossAsync(20m, prodB, catB, null, DateTime.UtcNow);

        Assert.Equal(1.11m, resB.UnitPriceGross);
    }
}
