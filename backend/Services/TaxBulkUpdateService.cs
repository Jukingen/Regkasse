using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Reassigns all tenant products from one tax group to another and appends <see cref="TaxHistory"/>
/// plus <see cref="ProductPriceHistory"/> / <see cref="ProductPriceVersion"/> rows.
/// </summary>
public sealed class TaxBulkUpdateService : ITaxBulkUpdateService
{
    private readonly AppDbContext _db;
    private readonly IProductPriceHistoryService _priceHistoryService;
    private readonly ILogger<TaxBulkUpdateService> _logger;

    public TaxBulkUpdateService(
        AppDbContext db,
        IProductPriceHistoryService priceHistoryService,
        ILogger<TaxBulkUpdateService> logger)
    {
        _db = db;
        _priceHistoryService = priceHistoryService;
        _logger = logger;
    }

    public async Task<TaxBulkUpdateResultDto> UpdateTaxForProductsAsync(
        Guid tenantId,
        Guid oldTaxGroupId,
        Guid newTaxGroupId,
        Guid changedBy,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (oldTaxGroupId == Guid.Empty)
            throw new ArgumentException("Old tax group id is required.", nameof(oldTaxGroupId));
        if (newTaxGroupId == Guid.Empty)
            throw new ArgumentException("New tax group id is required.", nameof(newTaxGroupId));
        if (oldTaxGroupId == newTaxGroupId)
            throw new InvalidOperationException("Old and new tax groups must be different.");

        var oldGroup = await _db.TaxGroups
            .FirstOrDefaultAsync(g => g.Id == oldTaxGroupId && g.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (oldGroup == null)
            throw new KeyNotFoundException("Old tax group not found.");

        var newGroup = await _db.TaxGroups
            .FirstOrDefaultAsync(
                g => g.Id == newTaxGroupId && g.TenantId == tenantId && g.IsActive,
                cancellationToken)
            .ConfigureAwait(false);
        if (newGroup == null)
            throw new KeyNotFoundException("New tax group not found.");

        var affectedProducts = await _db.Products
            .Where(p => p.TenantId == tenantId && p.TaxGroupId == oldTaxGroupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var oldRate = decimal.Round(oldGroup.Rate, 2, MidpointRounding.AwayFromZero);
        var newRate = decimal.Round(newGroup.Rate, 2, MidpointRounding.AwayFromZero);
        var historyReason = string.IsNullOrWhiteSpace(reason)
            ? $"Bulk tax group update: {oldGroup.Name} → {newGroup.Name}"
            : reason.Trim();
        var now = DateTime.UtcNow;
        var newTaxType = TaxTypes.FromTaxGroup(newGroup);

        foreach (var product in affectedProducts)
        {
            var previousPrice = product.Price;
            var previousTaxGroupId = product.TaxGroupId;
            var previousRate = decimal.Round(product.TaxRate, 2, MidpointRounding.AwayFromZero);

            product.TaxGroupId = newTaxGroupId;
            product.TaxRate = newRate;
            product.TaxType = newTaxType;
            product.UpdatedAt = now;

            _db.TaxHistories.Add(new TaxHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = product.Id,
                TaxGroupId = newTaxGroupId,
                OldRate = oldRate,
                NewRate = newRate,
                ChangedAt = now,
                ChangedBy = changedBy,
                Reason = historyReason,
            });

            await _priceHistoryService.RecordChangeAsync(
                tenantId,
                product.Id,
                previousPrice,
                previousPrice,
                previousTaxGroupId,
                newTaxGroupId,
                previousRate,
                newRate,
                changedBy,
                historyReason,
                saveChanges: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Bulk tax update for tenant {TenantId}: {Count} products {OldRate}% → {NewRate}% ({OldGroup} → {NewGroup})",
            tenantId,
            affectedProducts.Count,
            oldRate,
            newRate,
            oldTaxGroupId,
            newTaxGroupId);

        return new TaxBulkUpdateResultDto
        {
            TotalProducts = affectedProducts.Count,
            UpdatedProducts = affectedProducts.Count,
            OldRate = oldRate,
            NewRate = newRate,
            OldTaxGroupId = oldTaxGroupId,
            NewTaxGroupId = newTaxGroupId,
        };
    }

    public async Task<TaxApplyToProductsResultDto> ApplyTaxGroupToProductsAsync(
        Guid tenantId,
        Guid taxGroupId,
        IReadOnlyList<Guid> productIds,
        Guid changedBy,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (taxGroupId == Guid.Empty)
            throw new ArgumentException("Tax group id is required.", nameof(taxGroupId));
        if (productIds is null || productIds.Count == 0)
            throw new ArgumentException("At least one product id is required.", nameof(productIds));

        var distinctIds = productIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctIds.Count == 0)
            throw new ArgumentException("At least one valid product id is required.", nameof(productIds));

        var newGroup = await _db.TaxGroups
            .FirstOrDefaultAsync(
                g => g.Id == taxGroupId && g.TenantId == tenantId && g.IsActive,
                cancellationToken)
            .ConfigureAwait(false);
        if (newGroup == null)
            throw new KeyNotFoundException("Tax group not found.");

        var products = await _db.Products
            .Where(p => p.TenantId == tenantId && distinctIds.Contains(p.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var foundIds = products.Select(p => p.Id).ToHashSet();
        var notFound = distinctIds.Count(id => !foundIds.Contains(id));
        var newRate = decimal.Round(newGroup.Rate, 2, MidpointRounding.AwayFromZero);
        var newTaxType = TaxTypes.FromTaxGroup(newGroup);
        var historyReason = string.IsNullOrWhiteSpace(reason)
            ? $"Quick tax assign: {newGroup.Name} ({newRate}%)"
            : reason.Trim();
        var now = DateTime.UtcNow;
        var updated = 0;
        var unchanged = 0;

        foreach (var product in products)
        {
            var previousRate = decimal.Round(product.TaxRate, 2, MidpointRounding.AwayFromZero);
            if (product.TaxGroupId == taxGroupId && previousRate == newRate)
            {
                unchanged++;
                continue;
            }

            var oldRate = previousRate;
            var previousPrice = product.Price;
            var previousTaxGroupId = product.TaxGroupId;
            product.TaxGroupId = taxGroupId;
            product.TaxRate = newRate;
            product.TaxType = newTaxType;
            product.UpdatedAt = now;

            _db.TaxHistories.Add(new TaxHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = product.Id,
                TaxGroupId = taxGroupId,
                OldRate = oldRate,
                NewRate = newRate,
                ChangedAt = now,
                ChangedBy = changedBy,
                Reason = historyReason,
            });

            await _priceHistoryService.RecordChangeAsync(
                tenantId,
                product.Id,
                previousPrice,
                previousPrice,
                previousTaxGroupId,
                taxGroupId,
                oldRate,
                newRate,
                changedBy,
                historyReason,
                saveChanges: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            updated++;
        }

        if (updated > 0)
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Quick tax assign for tenant {TenantId}: updated={Updated} unchanged={Unchanged} notFound={NotFound} rate={Rate}% group={TaxGroupId}",
            tenantId,
            updated,
            unchanged,
            notFound,
            newRate,
            taxGroupId);

        return new TaxApplyToProductsResultDto
        {
            RequestedCount = distinctIds.Count,
            UpdatedProducts = updated,
            UnchangedProducts = unchanged,
            NotFound = notFound,
            TaxGroupId = taxGroupId,
            NewRate = newRate,
        };
    }
}
