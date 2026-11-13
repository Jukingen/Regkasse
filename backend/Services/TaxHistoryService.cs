using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class TaxHistoryService : ITaxHistoryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TaxHistoryService> _logger;

    public TaxHistoryService(AppDbContext db, ILogger<TaxHistoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TaxHistory?> RecordChangeAsync(
        Guid tenantId,
        Guid productId,
        Guid taxGroupId,
        decimal oldRate,
        decimal newRate,
        Guid changedBy,
        string? reason = null,
        string? invoiceNumber = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (productId == Guid.Empty)
            throw new ArgumentException("Product id is required.", nameof(productId));
        if (taxGroupId == Guid.Empty)
            throw new ArgumentException("Tax group id is required.", nameof(taxGroupId));

        var oldNormalized = decimal.Round(oldRate, 2, MidpointRounding.AwayFromZero);
        var newNormalized = decimal.Round(newRate, 2, MidpointRounding.AwayFromZero);

        var entry = new TaxHistory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            TaxGroupId = taxGroupId,
            OldRate = oldNormalized,
            NewRate = newNormalized,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = changedBy == Guid.Empty ? Guid.Empty : changedBy,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Product tax group updated" : reason.Trim(),
            InvoiceNumber = string.IsNullOrWhiteSpace(invoiceNumber) ? null : invoiceNumber.Trim(),
        };

        _db.TaxHistories.Add(entry);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Tax history recorded for product {ProductId}: {OldRate}% → {NewRate}% (tenant {TenantId})",
            productId,
            oldNormalized,
            newNormalized,
            tenantId);

        return entry;
    }

    public async Task<IReadOnlyList<TaxHistoryItemDto>> GetHistoryAsync(
        Guid tenantId,
        Guid? productId = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        var limit = Math.Clamp(take, 1, 500);
        var query = _db.TaxHistories
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId);

        if (productId is { } pid && pid != Guid.Empty)
            query = query.Where(h => h.ProductId == pid);

        return await query
            .OrderByDescending(h => h.ChangedAt)
            .Take(limit)
            .Select(h => new TaxHistoryItemDto
            {
                Id = h.Id,
                ProductId = h.ProductId,
                ProductName = h.Product != null ? h.Product.Name : string.Empty,
                TaxGroupId = h.TaxGroupId,
                TaxGroupName = h.TaxGroup != null ? h.TaxGroup.Name : null,
                OldRate = h.OldRate,
                NewRate = h.NewRate,
                ChangedAt = h.ChangedAt,
                ChangedBy = h.ChangedBy,
                Reason = h.Reason,
                InvoiceNumber = h.InvoiceNumber,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
