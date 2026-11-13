using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Read-only impact preview for sensitive tenant changes (tax rate, currency, product prices).
/// Does not mutate data; historical fiscal rows are never rewritten.
/// </summary>
public sealed class ImpactSimulationService : IImpactSimulationService
{
    private readonly AppDbContext _db;

    public ImpactSimulationService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<ImpactReport> SimulateChangeAsync(
        Guid tenantId,
        ChangeType changeType,
        object newValue,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        ArgumentNullException.ThrowIfNull(newValue);

        return changeType switch
        {
            ChangeType.TaxRate => await SimulateTaxRateChangeAsync(
                tenantId,
                Convert.ToDecimal(newValue),
                currentRateOverride: null,
                cancellationToken).ConfigureAwait(false),
            ChangeType.Currency => await SimulateCurrencyChangeAsync(
                tenantId,
                Convert.ToString(newValue) ?? string.Empty,
                cancellationToken).ConfigureAwait(false),
            ChangeType.ProductPrice => await SimulatePriceChangeAsync(
                tenantId,
                CoercePriceUpdates(newValue),
                cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(changeType), changeType, "Unsupported change type."),
        };
    }

    /// <inheritdoc />
    public async Task<ImpactReport> SimulateTaxRateChangeAsync(
        Guid tenantId,
        decimal newRate,
        decimal? currentRateOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (newRate is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(newRate), "Tax rate must be between 0 and 100.");

        var currentRate = currentRateOverride
            ?? await GetCurrentTaxRateAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var productsQuery = ProductsForTenant(tenantId)
            .Where(p => p.IsActive && p.TaxRate == currentRate);

        var affectedProducts = await productsQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var catalogSum = await productsQuery.SumAsync(p => (decimal?)p.Price, cancellationToken).ConfigureAwait(false)
            ?? 0m;

        var affectedPayments = await CountPaymentsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var affectedInvoices = await CountInvoicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var currency = await GetTenantCurrencyAsync(tenantId, cancellationToken).ConfigureAwait(false);

        // Catalog-level estimate: one-unit sale of each matching product (not a P&L projection).
        var financialImpact = CalculateTaxRateCatalogImpact(catalogSum, currentRate, newRate);

        var warnings = new List<string>();
        if (affectedProducts > 0)
            warnings.Add($"{affectedProducts} product(s) currently use tax rate {currentRate}% and would need review.");
        if (affectedPayments > 0)
            warnings.Add($"{affectedPayments} historical payment(s) will remain unchanged (no backdating).");
        if (Math.Abs(newRate - currentRate) >= 5)
            warnings.Add("Large tax rate delta (>= 5 percentage points). Confirm fiscal/legal approval before applying.");

        return new ImpactReport
        {
            Title = "Tax Rate Change Impact",
            Summary = $"Changing tax rate from {currentRate}% to {newRate}%",
            ChangeType = ChangeType.TaxRate,
            TenantId = tenantId,
            AffectedRecords = new ImpactAffectedRecordsDto
            {
                Products = affectedProducts,
                Payments = affectedPayments,
                Invoices = affectedInvoices,
            },
            EstimatedFinancialImpact = financialImpact,
            EstimatedFinancialImpactCurrency = currency,
            Recommendations =
            [
                "Affected products will need tax rate (and possibly price) updates.",
                "Historical payment and invoice data will remain unchanged.",
                "New invoices and receipts will use the new rate after products are updated.",
            ],
            Warnings = warnings,
            Severity = ResolveSeverity(warnings.Count, affectedProducts, affectedPayments),
        };
    }

    /// <inheritdoc />
    public async Task<ImpactReport> SimulateCurrencyChangeAsync(
        Guid tenantId,
        string newCurrency,
        CancellationToken cancellationToken = default)
    {
        var currency = (newCurrency ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(newCurrency));

        var currentCurrency = await GetTenantCurrencyAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var affectedProducts = await ProductsForTenant(tenantId)
            .Where(p => p.IsActive)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
        var affectedPayments = await CountPaymentsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var affectedInvoices = await CountInvoicesAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var warnings = new List<string>();
        if (string.Equals(currentCurrency, currency, StringComparison.OrdinalIgnoreCase))
            warnings.Add($"Proposed currency {currency} matches the current tenant currency.");
        if (affectedPayments > 0)
            warnings.Add($"Tenant has {affectedPayments} existing payment(s). Currency change will not affect historical data.");
        if (affectedInvoices > 0)
            warnings.Add($"Tenant has {affectedInvoices} invoice(s). Historical invoice amounts keep their original currency semantics.");

        return new ImpactReport
        {
            Title = "Currency Change Impact",
            Summary = $"Changing currency from {currentCurrency} to {currency}",
            ChangeType = ChangeType.Currency,
            TenantId = tenantId,
            AffectedRecords = new ImpactAffectedRecordsDto
            {
                Products = affectedProducts,
                Payments = affectedPayments,
                Invoices = affectedInvoices,
            },
            EstimatedFinancialImpact = null,
            EstimatedFinancialImpactCurrency = currency,
            Recommendations =
            [
                "Update product display prices if market pricing differs in the new currency.",
                "Historical payments and RKSV receipts remain as stored (no conversion / no backdating).",
                "Localization default currency should be kept in sync after approval.",
            ],
            Warnings = warnings,
            Severity = affectedPayments > 0 ? ImpactSeverity.Warning : ImpactSeverity.Info,
        };
    }

    /// <inheritdoc />
    public async Task<ImpactReport> SimulatePriceChangeAsync(
        Guid tenantId,
        IReadOnlyList<ProductPriceUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        if (updates.Count == 0)
            throw new ArgumentException("At least one product price update is required.", nameof(updates));
        if (updates.Count > 500)
            throw new ArgumentException("A maximum of 500 product price updates can be simulated at once.", nameof(updates));

        var ids = updates.Select(u => u.ProductId).Distinct().ToList();
        var products = await ProductsForTenant(tenantId)
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Price, p.IsActive })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byId = products.ToDictionary(p => p.Id);
        var matched = 0;
        var missing = 0;
        var inactive = 0;
        decimal deltaSum = 0m;

        foreach (var update in updates)
        {
            if (!byId.TryGetValue(update.ProductId, out var product))
            {
                missing++;
                continue;
            }

            matched++;
            if (!product.IsActive)
                inactive++;
            deltaSum += update.NewPrice - product.Price;
        }

        var affectedPayments = await CountPaymentsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var affectedInvoices = await CountInvoicesAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var currency = await GetTenantCurrencyAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var warnings = new List<string>();
        if (missing > 0)
            warnings.Add($"{missing} product id(s) were not found in this tenant and were skipped.");
        if (inactive > 0)
            warnings.Add($"{inactive} matched product(s) are inactive.");
        if (Math.Abs(deltaSum) >= 1000m)
            warnings.Add($"Large total catalog price delta ({deltaSum:0.##} {currency}). Review before applying.");

        return new ImpactReport
        {
            Title = "Product Price Change Impact",
            Summary = $"Simulating price updates for {updates.Count} product row(s) ({matched} matched)",
            ChangeType = ChangeType.ProductPrice,
            TenantId = tenantId,
            AffectedRecords = new ImpactAffectedRecordsDto
            {
                Products = matched,
                Payments = affectedPayments,
                Invoices = affectedInvoices,
            },
            EstimatedFinancialImpact = Math.Round(deltaSum, 2, MidpointRounding.AwayFromZero),
            EstimatedFinancialImpactCurrency = currency,
            Recommendations =
            [
                "Price changes apply to future sales only.",
                "Open carts / offline queues may still hold old prices until refreshed.",
                "Historical payments and invoices remain unchanged.",
            ],
            Warnings = warnings,
            Severity = ResolveSeverity(warnings.Count, matched, 0),
        };
    }

    private async Task<decimal> GetCurrentTaxRateAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var top = await ProductsForTenant(tenantId)
            .Where(p => p.IsActive)
            .GroupBy(p => p.TaxRate)
            .Select(g => new { Rate = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return top?.Rate ?? TaxTypes.GetTaxRate(TaxTypes.Standard);
    }

    private async Task<string> GetTenantCurrencyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var currency = await _db.CompanySettings.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.Currency)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(currency) ? "EUR" : currency.Trim().ToUpperInvariant();
    }

    private IQueryable<Product> ProductsForTenant(Guid tenantId) =>
        _db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => p.TenantId == tenantId);

    private async Task<int> CountPaymentsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await (
                from p in _db.PaymentDetails.AsNoTracking().IgnoreQueryFilters()
                join cr in _db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
                    on p.CashRegisterId equals cr.Id
                where cr.TenantId == tenantId
                select p.Id)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<int> CountInvoicesAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.Invoices.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Approximate catalog VAT delta if each matching product sold once (gross prices assumed).
    /// </summary>
    internal static decimal CalculateTaxRateCatalogImpact(decimal catalogGrossSum, decimal currentRate, decimal newRate)
    {
        if (catalogGrossSum <= 0 || currentRate == newRate)
            return 0m;

        // Price is treated as gross (inclusive): extract net, re-apply new rate.
        var divisor = 100m + currentRate;
        if (divisor <= 0)
            return 0m;

        var net = catalogGrossSum * 100m / divisor;
        var newGross = net * (100m + newRate) / 100m;
        return Math.Round(newGross - catalogGrossSum, 2, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<ProductPriceUpdate> CoercePriceUpdates(object newValue)
    {
        if (newValue is IReadOnlyList<ProductPriceUpdate> typed)
            return typed;
        if (newValue is IEnumerable<ProductPriceUpdate> enumerable)
            return enumerable.ToList();
        if (newValue is IEnumerable<ProductPriceUpdateDto> dtos)
        {
            return dtos.Select(d => new ProductPriceUpdate
            {
                ProductId = d.ProductId,
                NewPrice = d.NewPrice,
            }).ToList();
        }

        throw new ArgumentException(
            "Product price simulation requires a list of ProductPriceUpdate values.",
            nameof(newValue));
    }

    private static string ResolveSeverity(int warningCount, int affectedProducts, int affectedPayments)
    {
        if (affectedPayments > 1000 || affectedProducts > 500 || warningCount >= 3)
            return ImpactSeverity.Critical;
        if (warningCount > 0 || affectedProducts > 0 || affectedPayments > 0)
            return ImpactSeverity.Warning;
        return ImpactSeverity.Info;
    }
}
