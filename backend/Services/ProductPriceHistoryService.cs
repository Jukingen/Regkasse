using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class ProductPriceHistoryService : IProductPriceHistoryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ProductPriceHistoryService> _logger;

    public ProductPriceHistoryService(AppDbContext db, ILogger<ProductPriceHistoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ProductPriceHistory?> RecordChangeAsync(
        Guid tenantId,
        Guid productId,
        decimal oldPrice,
        decimal newPrice,
        Guid oldTaxGroupId,
        Guid newTaxGroupId,
        decimal oldTaxRate,
        decimal newTaxRate,
        Guid changedBy,
        string? reason = null,
        bool isRksvCompliant = true,
        string? rksvNote = null,
        bool saveChanges = true,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantProduct(tenantId, productId);
        if (oldTaxGroupId == Guid.Empty)
            throw new ArgumentException("Old tax group id is required.", nameof(oldTaxGroupId));
        if (newTaxGroupId == Guid.Empty)
            throw new ArgumentException("New tax group id is required.", nameof(newTaxGroupId));

        var oldPriceN = RoundPrice(oldPrice);
        var newPriceN = RoundPrice(newPrice);
        var oldRateN = RoundRate(oldTaxRate);
        var newRateN = RoundRate(newTaxRate);

        if (oldPriceN == newPriceN && oldTaxGroupId == newTaxGroupId && oldRateN == newRateN)
            return null;

        var now = DateTime.UtcNow;
        await CloseActiveHistoryAsync(tenantId, productId, now, cancellationToken).ConfigureAwait(false);
        var nextVersion = await CloseCurrentVersionAndNextLabelAsync(tenantId, productId, now, cancellationToken)
            .ConfigureAwait(false);

        var entry = new ProductPriceHistory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            OldPrice = oldPriceN,
            NewPrice = newPriceN,
            OldTaxGroupId = oldTaxGroupId,
            NewTaxGroupId = newTaxGroupId,
            OldTaxRate = oldRateN,
            NewTaxRate = newRateN,
            EffectiveFrom = now,
            EffectiveTo = null,
            IsActive = true,
            ChangedBy = changedBy == Guid.Empty ? Guid.Empty : changedBy,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Product price or tax updated" : reason.Trim(),
            CreatedAt = now,
            IsRksvCompliant = isRksvCompliant,
            RksvNote = string.IsNullOrWhiteSpace(rksvNote) ? null : rksvNote.Trim(),
            RksvVerifiedAt = isRksvCompliant ? now : null,
        };

        _db.ProductPriceHistories.Add(entry);
        _db.ProductPriceVersions.Add(new ProductPriceVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            Price = newPriceN,
            TaxGroupId = newTaxGroupId,
            ValidFrom = now,
            ValidTo = null,
            IsCurrent = true,
            Version = nextVersion,
            CreatedAt = now,
        });

        if (saveChanges)
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Price history recorded for product {ProductId}: price {OldPrice} → {NewPrice}, tax {OldRate}% → {NewRate}% (tenant {TenantId})",
            productId,
            oldPriceN,
            newPriceN,
            oldRateN,
            newRateN,
            tenantId);

        return entry;
    }

    public async Task EnsureInitialVersionAsync(
        Guid tenantId,
        Guid productId,
        decimal price,
        Guid taxGroupId,
        decimal taxRate,
        Guid changedBy,
        string? reason = null,
        bool saveChanges = true,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantProduct(tenantId, productId);
        if (taxGroupId == Guid.Empty)
            throw new ArgumentException("Tax group id is required.", nameof(taxGroupId));

        var exists = await _db.ProductPriceVersions
            .AnyAsync(v => v.TenantId == tenantId && v.ProductId == productId, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            return;

        var now = DateTime.UtcNow;
        var priceN = RoundPrice(price);
        var rateN = RoundRate(taxRate);

        _db.ProductPriceVersions.Add(new ProductPriceVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            Price = priceN,
            TaxGroupId = taxGroupId,
            ValidFrom = now,
            ValidTo = null,
            IsCurrent = true,
            Version = "1.0",
            CreatedAt = now,
        });

        _db.ProductPriceHistories.Add(new ProductPriceHistory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            OldPrice = priceN,
            NewPrice = priceN,
            OldTaxGroupId = taxGroupId,
            NewTaxGroupId = taxGroupId,
            OldTaxRate = rateN,
            NewTaxRate = rateN,
            EffectiveFrom = now,
            EffectiveTo = null,
            IsActive = true,
            ChangedBy = changedBy == Guid.Empty ? Guid.Empty : changedBy,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Initial product price" : reason.Trim(),
            CreatedAt = now,
            IsRksvCompliant = true,
            RksvVerifiedAt = now,
        });

        if (saveChanges)
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProductPriceHistoryItemDto>> GetHistoryAsync(
        Guid tenantId,
        Guid? productId = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        var limit = Math.Clamp(take, 1, 500);
        var query = _db.ProductPriceHistories
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId);

        if (productId is { } pid && pid != Guid.Empty)
            query = query.Where(h => h.ProductId == pid);

        return await query
            .OrderByDescending(h => h.EffectiveFrom)
            .Take(limit)
            .Select(h => new ProductPriceHistoryItemDto
            {
                Id = h.Id,
                ProductId = h.ProductId,
                ProductName = h.Product != null ? h.Product.Name : string.Empty,
                OldPrice = h.OldPrice,
                NewPrice = h.NewPrice,
                OldTaxGroupId = h.OldTaxGroupId,
                OldTaxGroupName = h.OldTaxGroup != null ? h.OldTaxGroup.Name : null,
                NewTaxGroupId = h.NewTaxGroupId,
                NewTaxGroupName = h.NewTaxGroup != null ? h.NewTaxGroup.Name : null,
                OldTaxRate = h.OldTaxRate,
                NewTaxRate = h.NewTaxRate,
                EffectiveFrom = h.EffectiveFrom,
                EffectiveTo = h.EffectiveTo,
                IsActive = h.IsActive,
                ChangedBy = h.ChangedBy,
                Reason = h.Reason,
                CreatedAt = h.CreatedAt,
                IsRksvCompliant = h.IsRksvCompliant,
                RksvNote = h.RksvNote,
                RksvVerifiedAt = h.RksvVerifiedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProductPriceVersionItemDto>> GetVersionsAsync(
        Guid tenantId,
        Guid productId,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantProduct(tenantId, productId);
        var limit = Math.Clamp(take, 1, 500);

        return await _db.ProductPriceVersions
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.ProductId == productId)
            .OrderByDescending(v => v.ValidFrom)
            .Take(limit)
            .Select(v => new ProductPriceVersionItemDto
            {
                Id = v.Id,
                ProductId = v.ProductId,
                ProductName = v.Product != null ? v.Product.Name : string.Empty,
                Price = v.Price,
                TaxGroupId = v.TaxGroupId,
                TaxGroupName = v.TaxGroup != null ? v.TaxGroup.Name : null,
                ValidFrom = v.ValidFrom,
                ValidTo = v.ValidTo,
                IsCurrent = v.IsCurrent,
                Version = v.Version ?? string.Empty,
                CreatedAt = v.CreatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProductPriceVersionItemDto?> GetCurrentVersionAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantProduct(tenantId, productId);

        return await _db.ProductPriceVersions
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.ProductId == productId && v.IsCurrent)
            .OrderByDescending(v => v.ValidFrom)
            .Select(v => new ProductPriceVersionItemDto
            {
                Id = v.Id,
                ProductId = v.ProductId,
                ProductName = v.Product != null ? v.Product.Name : string.Empty,
                Price = v.Price,
                TaxGroupId = v.TaxGroupId,
                TaxGroupName = v.TaxGroup != null ? v.TaxGroup.Name : null,
                ValidFrom = v.ValidFrom,
                ValidTo = v.ValidTo,
                IsCurrent = v.IsCurrent,
                Version = v.Version ?? string.Empty,
                CreatedAt = v.CreatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task CloseActiveHistoryAsync(
        Guid tenantId,
        Guid productId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var active = await _db.ProductPriceHistories
            .Where(h => h.TenantId == tenantId && h.ProductId == productId && h.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in active)
        {
            row.IsActive = false;
            row.EffectiveTo = now;
        }
    }

    private async Task<string> CloseCurrentVersionAndNextLabelAsync(
        Guid tenantId,
        Guid productId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var current = await _db.ProductPriceVersions
            .Where(v => v.TenantId == tenantId && v.ProductId == productId && v.IsCurrent)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string? latestLabel = null;
        foreach (var row in current)
        {
            row.IsCurrent = false;
            row.ValidTo = now;
            latestLabel ??= row.Version;
        }

        if (latestLabel is null)
        {
            latestLabel = await _db.ProductPriceVersions
                .Where(v => v.TenantId == tenantId && v.ProductId == productId)
                .OrderByDescending(v => v.ValidFrom)
                .Select(v => v.Version)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return BumpVersion(latestLabel);
    }

    private static string BumpVersion(string? current)
    {
        if (string.IsNullOrWhiteSpace(current))
            return "1.0";

        var trimmed = current.Trim();
        var majorPart = trimmed.Split('.', 2)[0];
        if (int.TryParse(majorPart, out var major) && major >= 0)
            return $"{major + 1}.0";

        return "1.0";
    }

    private static void ValidateTenantProduct(Guid tenantId, Guid productId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (productId == Guid.Empty)
            throw new ArgumentException("Product id is required.", nameof(productId));
    }

    private static decimal RoundPrice(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundRate(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
