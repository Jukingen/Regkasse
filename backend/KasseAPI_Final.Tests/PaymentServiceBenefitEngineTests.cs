using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Coverage for EvaluateBenefitsCoreAsync / ApplyBenefitUsageMutationsAsync.
/// Supported kinds: PercentageDiscount, FreeAllowance, BuyXGetY.
/// There is no FreeShipping or fixed-amount discount kind in PaymentService.
/// </summary>
public sealed class PaymentServiceBenefitEngineTests
{
    [Fact]
    public async Task Preview_WhenNoBenefits_ReturnsEmptyMatches()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var preview = await sut.ComputeBenefitEligibilityPreviewAsync(new BenefitEligibilityPreviewRequest
        {
            CustomerId = customerId,
            CashRegisterId = registerId,
            Items = [new BenefitEligibilityPreviewItemRequest { ProductId = productId, Quantity = 1 }]
        });

        Assert.NotNull(preview);
        Assert.Equal(10m, preview!.SubtotalBeforeBenefits);
        Assert.Empty(preview.ApplicableBenefits);
        Assert.Empty(preview.BlockedBenefits);
    }

    [Fact]
    public async Task CreatePayment_WhenPercentageDiscountAssigned_AppliesHighestPriority()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, categoryId) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await SeedPercentageAssignmentsAsync(ctx, customerId, lowPct: 10m, highPct: 20m);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 8m),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(8m, result.Payment!.TotalAmount);
        Assert.NotNull(result.Payment.AppliedBenefitsSnapshot);
        _ = categoryId;
    }

    [Fact]
    public async Task Preview_WhenPercentageAssignmentExpired_SkipsAndUsesCustomerFallback()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) =
            await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx, customerDiscountPercentage: 5m);
        var def = new BenefitDefinition
        {
            Code = "PCT-EXPIRED",
            Name = "Expired 50%",
            BenefitKind = AppliedBenefitKind.PercentageDiscount,
            PercentageValue = 50m
        };
        ctx.BenefitDefinitions.Add(def);
        ctx.BenefitAssignments.Add(new BenefitAssignment
        {
            BenefitDefinitionId = def.Id,
            CustomerId = customerId,
            ValidFrom = DateTime.UtcNow.AddDays(-30),
            ValidTo = DateTime.UtcNow.AddDays(-1),
            Priority = 100
        });
        await ctx.SaveChangesAsync();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var preview = await sut.ComputeBenefitEligibilityPreviewAsync(new BenefitEligibilityPreviewRequest
        {
            CustomerId = customerId,
            CashRegisterId = registerId,
            Items = [new BenefitEligibilityPreviewItemRequest { ProductId = productId, Quantity = 1 }]
        });

        Assert.NotNull(preview);
        Assert.Single(preview!.ApplicableBenefits);
        Assert.Equal(AppliedBenefitKind.PercentageDiscount, preview.ApplicableBenefits[0].Kind);
        Assert.Equal(-0.50m, preview.ApplicableBenefits[0].Amount);
    }

    [Fact]
    public async Task CreatePayment_WhenFreeAllowanceApplies_UpdatesDailyUsage()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, categoryId) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var defId = await SeedFreeAllowanceAsync(ctx, customerId, categoryId, allowanceQty: 1);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 0m),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(0m, result.Payment!.TotalAmount);
        var usage = await ctx.BenefitDailyUsages.AsNoTracking()
            .SingleAsync(u => u.CustomerId == customerId && u.BenefitDefinitionId == defId);
        Assert.Equal(1, usage.QuantityUsed);
    }

    [Fact]
    public async Task CreatePayment_WhenFreeAllowanceLimitAlreadyUsed_DoesNotApplyDiscount()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, categoryId) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var defId = await SeedFreeAllowanceAsync(ctx, customerId, categoryId, allowanceQty: 1);
        ctx.BenefitDailyUsages.Add(new BenefitDailyUsage
        {
            CustomerId = customerId,
            BenefitDefinitionId = defId,
            UsageDate = DateTime.UtcNow.Date,
            QuantityUsed = 1,
            Version = 1
        });
        await ctx.SaveChangesAsync();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var preview = await sut.ComputeBenefitEligibilityPreviewAsync(new BenefitEligibilityPreviewRequest
        {
            CustomerId = customerId,
            CashRegisterId = registerId,
            Items = [new BenefitEligibilityPreviewItemRequest { ProductId = productId, Quantity = 1 }]
        });
        Assert.Contains(preview!.BlockedBenefits, b => b.BlockedReasonCode == BenefitBlockedReasonCodes.DailyLimitReached);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 10m),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(10m, result.Payment!.TotalAmount);
        Assert.Equal(1, await ctx.BenefitDailyUsages.CountAsync());
    }

    [Fact]
    public async Task Preview_WhenFreeAllowancePartialRemaining_ClaimsOnlyRemaining()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, categoryId) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var defId = await SeedFreeAllowanceAsync(ctx, customerId, categoryId, allowanceQty: 2);
        ctx.BenefitDailyUsages.Add(new BenefitDailyUsage
        {
            CustomerId = customerId,
            BenefitDefinitionId = defId,
            UsageDate = DateTime.UtcNow.Date,
            QuantityUsed = 1,
            Version = 1
        });
        await ctx.SaveChangesAsync();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var preview = await sut.ComputeBenefitEligibilityPreviewAsync(new BenefitEligibilityPreviewRequest
        {
            CustomerId = customerId,
            CashRegisterId = registerId,
            Items = [new BenefitEligibilityPreviewItemRequest { ProductId = productId, Quantity = 2 }]
        });

        Assert.Single(preview!.ApplicableBenefits);
        Assert.Equal(1, preview.ApplicableBenefits[0].Quantity);
        Assert.Equal(-10m, preview.ApplicableBenefits[0].Amount);
    }

    [Fact]
    public async Task CreatePayment_WhenBenefitTotalMismatch_RollsBackUsageMutation()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, categoryId) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await SeedFreeAllowanceAsync(ctx, customerId, categoryId, allowanceQty: 1);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 10m),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Contains("Total amount mismatch", result.Message);
        Assert.Equal(0, await ctx.BenefitDailyUsages.CountAsync());
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
    }

    [Fact]
    public async Task Preview_WhenBuyXGetYQuantityNotReached_IsBlocked()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await SeedBuyXGetYAsync(ctx, customerId, buyX: 2, getY: 1);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var preview = await sut.ComputeBenefitEligibilityPreviewAsync(new BenefitEligibilityPreviewRequest
        {
            CustomerId = customerId,
            CashRegisterId = registerId,
            Items = [new BenefitEligibilityPreviewItemRequest { ProductId = productId, Quantity = 1 }]
        });

        Assert.Single(preview!.BlockedBenefits);
        Assert.Equal(BenefitBlockedReasonCodes.QuantityNotReached, preview.BlockedBenefits[0].BlockedReasonCode);
        Assert.Equal(1, preview.BlockedBenefits[0].RequiredMoreQuantity);
    }

    [Fact]
    public async Task CreatePayment_WhenBuyXGetYQualifies_AppliesFreeItem()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await SeedBuyXGetYAsync(ctx, customerId, buyX: 2, getY: 1);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId, total: 10m, quantity: 2),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(10m, result.Payment!.TotalAmount);
        Assert.NotNull(result.Payment.AppliedBenefitsSnapshot);
    }

    [Fact]
    public async Task Preview_WhenPercentageAndFreeAllowanceCombine_AppliesBoth()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, categoryId) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await SeedPercentageAssignmentsAsync(ctx, customerId, lowPct: 10m, highPct: 10m);
        await SeedFreeAllowanceAsync(ctx, customerId, categoryId, allowanceQty: 1);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);

        var preview = await sut.ComputeBenefitEligibilityPreviewAsync(new BenefitEligibilityPreviewRequest
        {
            CustomerId = customerId,
            CashRegisterId = registerId,
            Items = [new BenefitEligibilityPreviewItemRequest { ProductId = productId, Quantity = 2 }]
        });

        Assert.Equal(2, preview!.ApplicableBenefits.Count);
        Assert.Contains(preview.ApplicableBenefits, b => b.Kind == AppliedBenefitKind.PercentageDiscount);
        Assert.Contains(preview.ApplicableBenefits, b => b.Kind == AppliedBenefitKind.FreeAllowance);
        Assert.Equal(-12m, preview.TotalDiscountAmount);
    }

    private static async Task SeedPercentageAssignmentsAsync(
        KasseAPI_Final.Data.AppDbContext ctx,
        Guid customerId,
        decimal lowPct,
        decimal highPct)
    {
        var low = new BenefitDefinition
        {
            Code = "PCT-LOW",
            Name = "Low %",
            BenefitKind = AppliedBenefitKind.PercentageDiscount,
            PercentageValue = lowPct
        };
        var high = new BenefitDefinition
        {
            Code = "PCT-HIGH",
            Name = "High %",
            BenefitKind = AppliedBenefitKind.PercentageDiscount,
            PercentageValue = highPct
        };
        ctx.BenefitDefinitions.AddRange(low, high);
        ctx.BenefitAssignments.AddRange(
            new BenefitAssignment
            {
                BenefitDefinitionId = low.Id,
                CustomerId = customerId,
                ValidFrom = DateTime.UtcNow.AddDays(-1),
                Priority = 1
            },
            new BenefitAssignment
            {
                BenefitDefinitionId = high.Id,
                CustomerId = customerId,
                ValidFrom = DateTime.UtcNow.AddDays(-1),
                Priority = 10
            });
        await ctx.SaveChangesAsync();
    }

    private static async Task<Guid> SeedFreeAllowanceAsync(
        KasseAPI_Final.Data.AppDbContext ctx,
        Guid customerId,
        Guid categoryId,
        int allowanceQty)
    {
        var def = new BenefitDefinition
        {
            Code = "FREE-COFFEE",
            Name = "Free Speisen",
            BenefitKind = AppliedBenefitKind.FreeAllowance,
            AllowanceQuantity = allowanceQty,
            AllowanceScope = "per_day",
            AllowanceCategoryId = categoryId
        };
        ctx.BenefitDefinitions.Add(def);
        ctx.BenefitAssignments.Add(new BenefitAssignment
        {
            BenefitDefinitionId = def.Id,
            CustomerId = customerId,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            Priority = 1
        });
        await ctx.SaveChangesAsync();
        return def.Id;
    }

    private static async Task SeedBuyXGetYAsync(
        KasseAPI_Final.Data.AppDbContext ctx,
        Guid customerId,
        int buyX,
        int getY)
    {
        var def = new BenefitDefinition
        {
            Code = "BOGO",
            Name = "Buy X get Y",
            BenefitKind = AppliedBenefitKind.BuyXGetY,
            BuyXQuantity = buyX,
            GetYQuantity = getY
        };
        ctx.BenefitDefinitions.Add(def);
        ctx.BenefitAssignments.Add(new BenefitAssignment
        {
            BenefitDefinitionId = def.Id,
            CustomerId = customerId,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            Priority = 1
        });
        await ctx.SaveChangesAsync();
    }
}
