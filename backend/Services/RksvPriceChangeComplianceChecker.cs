using System.Globalization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// RKSV rules for product price / tax changes (versioning, allowed rates, audit logging).
/// </summary>
public sealed class RksvPriceChangeComplianceChecker : IRksvPriceChangeComplianceChecker
{
    public const string CodeRequiresNewVersion = "RKSV_001";
    public const string CodeInvalidTaxRate = "RKSV_002";
    public const string CodeAuditTrailRequired = "RKSV_003";
    public const string CodeProductNotFound = "RKSV_004";
    public const string CodeTaxGroupNotFound = "RKSV_005";
    public const string CodeArchivedProduct = "RKSV_006";

    private readonly AppDbContext _db;
    private readonly ITaxRegulationService _regulation;
    private readonly ILogger<RksvPriceChangeComplianceChecker> _logger;

    public RksvPriceChangeComplianceChecker(
        AppDbContext db,
        ITaxRegulationService regulation,
        ILogger<RksvPriceChangeComplianceChecker> logger)
    {
        _db = db;
        _regulation = regulation;
        _logger = logger;
    }

    public async Task<RksvPriceChangeComplianceResult> CheckPriceChangeComplianceAsync(
        Guid tenantId,
        Guid productId,
        decimal newPrice,
        Guid newTaxGroupId,
        bool forceInPlaceUpdate = false,
        CancellationToken cancellationToken = default)
    {
        var result = new RksvPriceChangeComplianceResult();

        if (tenantId == Guid.Empty)
        {
            result.Errors.Add(Finding(
                CodeProductNotFound,
                "Tenant id is required.",
                "Provide a valid tenant context."));
            result.IsCompliant = false;
            return result;
        }

        if (productId == Guid.Empty)
        {
            result.Errors.Add(Finding(
                CodeProductNotFound,
                "Product id is required.",
                "Select a valid product."));
            result.IsCompliant = false;
            return result;
        }

        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (product is null)
        {
            result.Errors.Add(Finding(
                CodeProductNotFound,
                "Product not found.",
                "Refresh the product list and try again."));
            result.IsCompliant = false;
            return result;
        }

        if (!product.IsActive || product.ArchivedAt is not null)
        {
            result.Errors.Add(Finding(
                CodeArchivedProduct,
                "Cannot change price on an archived product.",
                "Open the current (active) catalog version of this product."));
            result.IsCompliant = false;
            return result;
        }

        var taxGroup = await _db.TaxGroups.AsNoTracking()
            .FirstOrDefaultAsync(
                g => g.Id == newTaxGroupId && g.TenantId == tenantId,
                cancellationToken)
            .ConfigureAwait(false);

        if (taxGroup is null || !taxGroup.IsActive)
        {
            result.Errors.Add(Finding(
                CodeTaxGroupNotFound,
                "Tax group not found or inactive.",
                "Select an active Austrian MwSt tax group."));
            result.IsCompliant = false;
            return result;
        }

        var newTaxRate = decimal.Round(taxGroup.Rate, 2, MidpointRounding.AwayFromZero);
        result.NewTaxRate = newTaxRate;

        var oldPrice = decimal.Round(product.Price, 2, MidpointRounding.AwayFromZero);
        var proposedPrice = decimal.Round(newPrice, 2, MidpointRounding.AwayFromZero);
        var priceOrTaxChanging =
            oldPrice != proposedPrice
            || product.TaxGroupId != newTaxGroupId
            || decimal.Round(product.TaxRate, 2, MidpointRounding.AwayFromZero) != newTaxRate;

        // Receipt rows do not store ProductId; order lines retain the sold product id.
        var hasFiscalHistory = await _db.OrderItems.AsNoTracking()
            .AnyAsync(oi => oi.ProductId == productId, cancellationToken)
            .ConfigureAwait(false);
        result.HasFiscalHistory = hasFiscalHistory;

        // Rule 1: fiscal history → new catalog version (unless forced in-place).
        if (hasFiscalHistory && priceOrTaxChanging)
        {
            result.RequiresNewProductVersion = !forceInPlaceUpdate;
            result.Warnings.Add(Finding(
                CodeRequiresNewVersion,
                forceInPlaceUpdate
                    ? "Product has existing RKSV sales history. In-place update was forced; historical receipts keep their original amounts."
                    : "Product has existing RKSV receipts. Creating new product version.",
                forceInPlaceUpdate
                    ? "Prefer creating a new product version unless you intentionally force an in-place update."
                    : "A new product version will be created. Existing receipts remain unchanged."));
        }

        // Rule 2: tax rate must be in current Austrian RKSV / MwSt bands.
        var rateValid = await _regulation.IsTaxRateValidAsync(newTaxRate, cancellationToken)
            .ConfigureAwait(false);
        if (!rateValid)
        {
            var regulation = await _regulation.GetCurrentRegulationAsync(cancellationToken)
                .ConfigureAwait(false);
            var allowed = string.Join(
                ", ",
                TaxRegulationService.GetDistinctAllowedRates(regulation)
                    .OrderBy(r => r)
                    .Select(r => $"{r.ToString("0.##", CultureInfo.InvariantCulture)}%"));

            result.Errors.Add(Finding(
                CodeInvalidTaxRate,
                $"Tax rate {newTaxRate.ToString("0.##", CultureInfo.InvariantCulture)}% is not valid for RKSV.",
                $"Please select a valid RKSV tax rate ({allowed})."));
        }

        // Rule 3: price/tax change must be logged (system obligation — always listed).
        if (priceOrTaxChanging)
        {
            result.Requirements.Add(Finding(
                CodeAuditTrailRequired,
                "Price change must be logged for audit trail.",
                "System will automatically log the change with timestamp, user, and reason."));
        }

        result.IsCompliant = result.Errors.Count == 0;

        _logger.LogInformation(
            "RKSV price-change compliance product {ProductId}: compliant={Compliant} fiscal={Fiscal} newVersion={NewVersion} errors={Errors} warnings={Warnings}",
            productId,
            result.IsCompliant,
            result.HasFiscalHistory,
            result.RequiresNewProductVersion,
            result.Errors.Count,
            result.Warnings.Count);

        return result;
    }

    private static RksvComplianceFinding Finding(string code, string message, string resolution) =>
        new()
        {
            Code = code,
            Message = message,
            Resolution = resolution,
        };
}
