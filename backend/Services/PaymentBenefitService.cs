using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public record BenefitCalculationResult(
    decimal TotalAmount,
    decimal TotalTaxAmount,
    Dictionary<string, decimal> TaxDetails,
    JsonDocument? AppliedBenefitsSnapshot,
    List<DailyUsageDelta> UsageDeltas
);

public record DailyUsageDelta(
    Guid CustomerId,
    Guid BenefitDefinitionId,
    DateTime UsageDate,
    int ClaimedQuantity
);

public record SharedBenefitEvaluation(
    decimal FinalTotalAmount,
    List<EvaluatedBenefit> Matches
);

public record EvaluatedBenefit(
    AppliedBenefitKind Kind,
    string? DefinitionCode,
    Guid? BenefitDefinitionId,
    decimal DiscountAmount,
    int? ClaimedQuantity,
    bool IsBlocked,
    string? BlockedReasonCode,
    string? BlockedMessage,
    int? RequiredMoreQuantity,
    string? Description
);

/// <summary>
/// Benefit preview and sale-time discount calculation.
/// Usage rows are added to the caller context and are not saved here.
/// </summary>
public interface IPaymentBenefitService
{
    Task<BenefitEligibilityPreviewResponse?> ComputeBenefitEligibilityPreviewAsync(BenefitEligibilityPreviewRequest request);

    Task<BenefitCalculationResult> CalculateBenefitsAsync(
        Customer customer,
        List<PaymentItem> paymentItems,
        Dictionary<Guid, Guid> productIdToCategoryId,
        decimal initialTotalAmount,
        decimal initialTotalTaxAmount,
        Dictionary<string, decimal> initialTaxDetails);

    Task ApplyBenefitUsageMutationsAsync(List<DailyUsageDelta> deltas);
}

public sealed class PaymentBenefitService : IPaymentBenefitService
{
    private readonly AppDbContext _context;
    private readonly IGenericRepository<Customer> _customerRepository;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ICountryStrategyContext _countryStrategyContext;
    private readonly ITaxStrategyResolver _taxStrategyResolver;
    private readonly IPricingRuleResolver _pricingRuleResolver;
    private readonly ICountryTaxTypeRegistry _taxTypes;
    private readonly IChMwstEffectiveRates? _chMwstRates;

    public PaymentBenefitService(
        AppDbContext context,
        IGenericRepository<Customer> customerRepository,
        ISettingsTenantResolver settingsTenantResolver,
        ICountryStrategyContext countryStrategyContext,
        ITaxStrategyResolver taxStrategyResolver,
        IPricingRuleResolver pricingRuleResolver,
        ICountryTaxTypeRegistry taxTypes,
        IChMwstEffectiveRates? chMwstRates)
    {
        _context = context;
        _customerRepository = customerRepository;
        _settingsTenantResolver = settingsTenantResolver;
        _countryStrategyContext = countryStrategyContext;
        _taxStrategyResolver = taxStrategyResolver;
        _pricingRuleResolver = pricingRuleResolver;
        _taxTypes = taxTypes;
        _chMwstRates = chMwstRates;
    }

    public async Task<BenefitEligibilityPreviewResponse?> ComputeBenefitEligibilityPreviewAsync(BenefitEligibilityPreviewRequest request)
    {
        if (request == null || request.CustomerId == Guid.Empty)
            return null;

        var customer = await _customerRepository.GetByIdAsync(request.CustomerId);
        if (customer == null || !customer.IsActive)
            return null;

        var effectiveTenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync();
        var countryBinding = await _countryStrategyContext.LoadAsync().ConfigureAwait(false);
        var taxStrategy = _taxStrategyResolver.Resolve(countryBinding.Profile, countryBinding.VatRegime);
        var paymentItems = new List<PaymentItem>();
        var productIdToCategoryId = new Dictionary<Guid, Guid>();
        decimal totalAmount = 0;

        var pricedLines = new List<(BenefitEligibilityPreviewItemRequest Item, Product Product, decimal UnitGross)>();
        foreach (var item in request.Items ?? new List<BenefitEligibilityPreviewItemRequest>())
        {
            if (item.Quantity < 1)
                continue;
            var product = await _context.Products
                .Include(p => p.CategoryNavigation)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == item.ProductId && p.TenantId == effectiveTenantId);
            if (product?.CategoryNavigation == null)
                continue;

            var priceRes = await _pricingRuleResolver.ResolveUnitGrossAsync(
                product.Price,
                product.Id,
                product.CategoryId,
                request.CashRegisterId,
                DateTime.UtcNow);
            pricedLines.Add((item, product, priceRes.UnitPriceGross));
        }

        var previewRates = TaxTypesFor(countryBinding);
        var taxResult = taxStrategy.CalculateTax(
            pricedLines.ConvertAll(l =>
                CountryPaymentTaxLineMapper.FromProductTaxType(
                    countryBinding.Profile,
                    l.UnitGross,
                    l.Item.Quantity,
                    l.Product.TaxType,
                    previewRates)),
            new TaxCalculationContext
            {
                CountryProfile = countryBinding.Profile,
                VatRegime = countryBinding.VatRegime,
                TaxExempt = countryBinding.Settings.TaxExempt,
                DestinationCountry = null,
                TenantId = countryBinding.Settings.TenantId,
            });

        for (var i = 0; i < pricedLines.Count; i++)
        {
            var (item, product, _) = pricedLines[i];
            var line = taxResult.Lines[i];
            totalAmount += line.LineGross;
            paymentItems.Add(new PaymentItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = line.UnitPriceGross,
                TotalPrice = line.LineGross,
                TaxType = line.TaxType,
                TaxRate = line.TaxRate,
                TaxAmount = line.LineTax,
                LineNet = line.LineNet
            });
            productIdToCategoryId[product.Id] = product.CategoryId;
        }

        var subtotalBeforeBenefits = totalAmount;
        var applicableList = new List<ApplicableBenefitPreviewDto>();
        var blockedList = new List<BlockedBenefitPreviewDto>();
        decimal totalDiscount = 0;
        var now = DateTime.UtcNow;

        var evaluation = await EvaluateBenefitsCoreAsync(customer, paymentItems, productIdToCategoryId, totalAmount, now);

        foreach (var match in evaluation.Matches)
        {
            if (match.IsBlocked)
            {
                blockedList.Add(new BlockedBenefitPreviewDto
                {
                    Kind = match.Kind,
                    BenefitDefinitionCode = match.DefinitionCode,
                    BlockedReasonCode = match.BlockedReasonCode,
                    Message = match.BlockedMessage,
                    RequiredMoreQuantity = match.RequiredMoreQuantity
                });
            }
            else
            {
                totalDiscount += match.DiscountAmount;
                applicableList.Add(new ApplicableBenefitPreviewDto
                {
                    Kind = match.Kind,
                    Description = match.Description,
                    Amount = -match.DiscountAmount,
                    Quantity = match.ClaimedQuantity,
                    BenefitDefinitionCode = match.DefinitionCode
                });
            }
        }

        return new BenefitEligibilityPreviewResponse
        {
            SubtotalBeforeBenefits = subtotalBeforeBenefits,
            TotalDiscountAmount = -totalDiscount,
            SubtotalAfterBenefits = totalAmount,
            ApplicableBenefits = applicableList,
            BlockedBenefits = blockedList
        };
    }

    private ICountryTaxTypeRegistry TaxTypesFor(CountryStrategyBinding binding)
    {
        if (_chMwstRates is null
            || !string.Equals(binding.Profile.Code, CountryProfileCodes.Switzerland, StringComparison.OrdinalIgnoreCase))
        {
            return _taxTypes;
        }

        return _chMwstRates.ForCalculation(_taxTypes, binding.Settings.TenantId);
    }

    private async Task<SharedBenefitEvaluation> EvaluateBenefitsCoreAsync(
        Customer customer,
        List<PaymentItem> paymentItems,
        Dictionary<Guid, Guid> productIdToCategoryId,
        decimal initialTotalAmount,
        DateTime now)
    {
        decimal currentTotalAmount = initialTotalAmount;
        var matches = new List<EvaluatedBenefit>();

        // Percentage discount: single highest-priority assignment or customer fallback
        decimal effectivePct = 0;
        var assignedPercentage = await _context.BenefitAssignments
            .AsNoTracking()
            .Where(ba => ba.CustomerId == customer.Id && ba.IsActive
                && ba.ValidFrom <= now && (ba.ValidTo == null || ba.ValidTo >= now))
            .Include(ba => ba.BenefitDefinition)
            .Where(ba => ba.BenefitDefinition.BenefitKind == AppliedBenefitKind.PercentageDiscount
                && ba.BenefitDefinition.IsActive
                && ba.BenefitDefinition.PercentageValue.HasValue
                && ba.BenefitDefinition.PercentageValue > 0)
            .OrderByDescending(ba => ba.Priority)
            .ThenByDescending(ba => ba.BenefitDefinition.PercentageValue)
            .Select(ba => new { ba.BenefitDefinition.PercentageValue, ba.BenefitDefinition.Code, ba.BenefitDefinition.Id })
            .FirstOrDefaultAsync();

        if (assignedPercentage != null && assignedPercentage.PercentageValue.HasValue && assignedPercentage.PercentageValue.Value > 0)
            effectivePct = assignedPercentage.PercentageValue.Value;
        else
            effectivePct = customer.DiscountPercentage;

        if (currentTotalAmount > 0 && effectivePct > 0)
        {
            effectivePct = Math.Clamp(effectivePct, 0, 100);
            var discountAmount = CartMoneyHelper.Round(currentTotalAmount * effectivePct / 100m);
            if (discountAmount > 0)
            {
                currentTotalAmount -= discountAmount;
                matches.Add(new EvaluatedBenefit(
                    Kind: AppliedBenefitKind.PercentageDiscount,
                    DefinitionCode: assignedPercentage?.Code,
                    BenefitDefinitionId: assignedPercentage?.Id,
                    DiscountAmount: discountAmount,
                    ClaimedQuantity: null,
                    IsBlocked: false,
                    BlockedReasonCode: null,
                    BlockedMessage: null,
                    RequiredMoreQuantity: null,
                    Description: $"Customer discount {effectivePct}%"
                ));
            }
        }

        // FreeAllowance
        var todayUtc = now.Date;
        var freeAllowanceAssignments = await _context.BenefitAssignments
            .AsNoTracking()
            .Where(ba => ba.CustomerId == customer.Id && ba.IsActive
                && ba.ValidFrom <= now && (ba.ValidTo == null || ba.ValidTo >= now))
            .Include(ba => ba.BenefitDefinition)
            .Where(ba => ba.BenefitDefinition.BenefitKind == AppliedBenefitKind.FreeAllowance
                && ba.BenefitDefinition.IsActive
                && ba.BenefitDefinition.AllowanceQuantity.HasValue
                && ba.BenefitDefinition.AllowanceQuantity > 0
                && ba.BenefitDefinition.AllowanceCategoryId.HasValue
                && (ba.BenefitDefinition.AllowanceScope == null || ba.BenefitDefinition.AllowanceScope.ToLower() == "per_day"))
            .OrderByDescending(ba => ba.Priority)
            .ToListAsync();

        foreach (var assignment in freeAllowanceAssignments)
        {
            var def = assignment.BenefitDefinition;
            var allowanceQty = def.AllowanceQuantity!.Value;
            var categoryId = def.AllowanceCategoryId!.Value;

            var usageRow = await _context.BenefitDailyUsages
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.CustomerId == customer.Id && u.BenefitDefinitionId == def.Id && u.UsageDate == todayUtc);
            var quantityUsed = usageRow?.QuantityUsed ?? 0;
            var remaining = allowanceQty - quantityUsed;

            if (remaining <= 0)
            {
                matches.Add(new EvaluatedBenefit(
                    Kind: AppliedBenefitKind.FreeAllowance,
                    DefinitionCode: def.Code,
                    BenefitDefinitionId: def.Id,
                    DiscountAmount: 0,
                    ClaimedQuantity: null,
                    IsBlocked: true,
                    BlockedReasonCode: BenefitBlockedReasonCodes.DailyLimitReached,
                    BlockedMessage: "Daily allowance limit reached",
                    RequiredMoreQuantity: null,
                    Description: null
                ));
                continue;
            }

            var eligibleItems = paymentItems.Where(pi => productIdToCategoryId.GetValueOrDefault(pi.ProductId) == categoryId).ToList();
            var eligibleQuantity = eligibleItems.Sum(i => i.Quantity);
            if (eligibleQuantity <= 0)
            {
                matches.Add(new EvaluatedBenefit(
                    Kind: AppliedBenefitKind.FreeAllowance,
                    DefinitionCode: def.Code,
                    BenefitDefinitionId: def.Id,
                    DiscountAmount: 0,
                    ClaimedQuantity: null,
                    IsBlocked: true,
                    BlockedReasonCode: BenefitBlockedReasonCodes.NoEligibleItems,
                    BlockedMessage: "No cart items in allowance category",
                    RequiredMoreQuantity: null,
                    Description: null
                ));
                continue;
            }

            var claimed = Math.Min(remaining, eligibleQuantity);
            var remainingToClaim = claimed;
            decimal discountAmount = 0;
            foreach (var pi in eligibleItems)
            {
                if (remainingToClaim <= 0)
                    break;
                var n = Math.Min(pi.Quantity, remainingToClaim);
                discountAmount += pi.UnitPrice * n;
                remainingToClaim -= n;
            }
            discountAmount = CartMoneyHelper.Round(discountAmount);
            if (discountAmount > 0)
            {
                currentTotalAmount -= discountAmount;
                matches.Add(new EvaluatedBenefit(
                    Kind: AppliedBenefitKind.FreeAllowance,
                    DefinitionCode: def.Code,
                    BenefitDefinitionId: def.Id,
                    DiscountAmount: discountAmount,
                    ClaimedQuantity: claimed,
                    IsBlocked: false,
                    BlockedReasonCode: null,
                    BlockedMessage: null,
                    RequiredMoreQuantity: null,
                    Description: $"Free allowance ({claimed} items)"
                ));
            }
        }

        // BuyXGetY
        var buyXGetYAssignment = await _context.BenefitAssignments
            .AsNoTracking()
            .Where(ba => ba.CustomerId == customer.Id && ba.IsActive
                && ba.ValidFrom <= now && (ba.ValidTo == null || ba.ValidTo >= now))
            .Include(ba => ba.BenefitDefinition)
            .Where(ba => ba.BenefitDefinition.BenefitKind == AppliedBenefitKind.BuyXGetY
                && ba.BenefitDefinition.IsActive
                && ba.BenefitDefinition.BuyXQuantity.HasValue
                && ba.BenefitDefinition.BuyXQuantity > 0
                && ba.BenefitDefinition.GetYQuantity.HasValue
                && ba.BenefitDefinition.GetYQuantity > 0)
            .OrderByDescending(ba => ba.Priority)
            .FirstOrDefaultAsync();

        if (buyXGetYAssignment?.BenefitDefinition != null)
        {
            var def = buyXGetYAssignment.BenefitDefinition;
            var buyX = def.BuyXQuantity!.Value;
            var getY = def.GetYQuantity!.Value;
            var totalQuantity = paymentItems.Sum(pi => pi.Quantity);

            if (totalQuantity < buyX)
            {
                matches.Add(new EvaluatedBenefit(
                    Kind: AppliedBenefitKind.BuyXGetY,
                    DefinitionCode: def.Code,
                    BenefitDefinitionId: def.Id,
                    DiscountAmount: 0,
                    ClaimedQuantity: null,
                    IsBlocked: true,
                    BlockedReasonCode: BenefitBlockedReasonCodes.QuantityNotReached,
                    BlockedMessage: $"Buy {buyX} get {getY} free: add {buyX - totalQuantity} more item(s) to qualify",
                    RequiredMoreQuantity: buyX - totalQuantity,
                    Description: null
                ));
            }
            else
            {
                var sets = totalQuantity / buyX;
                var freeQty = Math.Min(sets * getY, totalQuantity);
                if (freeQty > 0)
                {
                    var unitPrices = new List<decimal>();
                    foreach (var pi in paymentItems)
                    {
                        for (var i = 0; i < pi.Quantity; i++)
                            unitPrices.Add(pi.UnitPrice);
                    }
                    unitPrices.Sort();
                    var take = Math.Min(freeQty, unitPrices.Count);
                    var discountAmount = 0m;
                    for (var i = 0; i < take; i++)
                        discountAmount += unitPrices[i];
                    discountAmount = CartMoneyHelper.Round(discountAmount);
                    if (discountAmount > 0)
                    {
                        currentTotalAmount -= discountAmount;
                        matches.Add(new EvaluatedBenefit(
                            Kind: AppliedBenefitKind.BuyXGetY,
                            DefinitionCode: def.Code,
                            BenefitDefinitionId: def.Id,
                            DiscountAmount: discountAmount,
                            ClaimedQuantity: freeQty,
                            IsBlocked: false,
                            BlockedReasonCode: null,
                            BlockedMessage: null,
                            RequiredMoreQuantity: null,
                            Description: $"Buy {buyX} get {getY} free ({freeQty} items)"
                        ));
                    }
                }
            }
        }

        return new SharedBenefitEvaluation(currentTotalAmount, matches);
    }
    public async Task<BenefitCalculationResult> CalculateBenefitsAsync(
        Customer customer,
        List<PaymentItem> paymentItems,
        Dictionary<Guid, Guid> productIdToCategoryId,
        decimal initialTotalAmount,
        decimal initialTotalTaxAmount,
        Dictionary<string, decimal> initialTaxDetails)
    {
        decimal currentTotalAmount = initialTotalAmount;
        decimal totalTaxAmount = initialTotalTaxAmount;
        var taxDetails = new Dictionary<string, decimal>(initialTaxDetails);
        var usageDeltas = new List<DailyUsageDelta>();
        var snapshotList = new List<AppliedBenefitSnapshotItem>();
        var now = DateTime.UtcNow;

        var evaluation = await EvaluateBenefitsCoreAsync(customer, paymentItems, productIdToCategoryId, initialTotalAmount, now);

        foreach (var match in evaluation.Matches)
        {
            if (match.IsBlocked)
                continue;

            var totalBeforeDiscount = currentTotalAmount;
            currentTotalAmount -= match.DiscountAmount;

            var ratio = totalBeforeDiscount > 0 ? currentTotalAmount / totalBeforeDiscount : 1m;
            totalTaxAmount = CartMoneyHelper.Round(totalTaxAmount * ratio);
            var keys = taxDetails.Keys.ToList();
            foreach (var key in keys)
                taxDetails[key] = CartMoneyHelper.Round(taxDetails[key] * ratio);

            snapshotList.Add(new AppliedBenefitSnapshotItem
            {
                Kind = match.Kind,
                Description = match.Description,
                Amount = -match.DiscountAmount,
                Quantity = match.ClaimedQuantity
            });

            if (match.BenefitDefinitionId.HasValue && match.ClaimedQuantity.HasValue && match.Kind == AppliedBenefitKind.FreeAllowance)
            {
                usageDeltas.Add(new DailyUsageDelta(customer.Id, match.BenefitDefinitionId.Value, now.Date, match.ClaimedQuantity.Value));
            }
        }

        JsonDocument? appliedBenefitsSnapshot = null;
        if (snapshotList.Count > 0)
            appliedBenefitsSnapshot = JsonDocument.Parse(JsonSerializer.Serialize(snapshotList));

        return new BenefitCalculationResult(evaluation.FinalTotalAmount, totalTaxAmount, taxDetails, appliedBenefitsSnapshot, usageDeltas);
    }

    public async Task ApplyBenefitUsageMutationsAsync(List<DailyUsageDelta> deltas)
    {
        foreach (var delta in deltas)
        {
            var usageRow = await _context.BenefitDailyUsages
                .FirstOrDefaultAsync(u => u.CustomerId == delta.CustomerId && u.BenefitDefinitionId == delta.BenefitDefinitionId && u.UsageDate == delta.UsageDate);

            if (usageRow == null)
            {
                usageRow = new BenefitDailyUsage
                {
                    CustomerId = delta.CustomerId,
                    BenefitDefinitionId = delta.BenefitDefinitionId,
                    UsageDate = delta.UsageDate,
                    QuantityUsed = 0,
                    Version = 0
                };
                _context.BenefitDailyUsages.Add(usageRow);
            }

            usageRow.QuantityUsed += delta.ClaimedQuantity;
            usageRow.Version++;
        }
    }
}
