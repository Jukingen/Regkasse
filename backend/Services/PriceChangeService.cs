using System.Data;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

/// <summary>
/// Orchestrates catalog price/tax changes with append-only version + history rows (RKSV trail).
/// Does not rewrite historical payment line snapshots.
/// </summary>
public sealed class PriceChangeService : IPriceChangeService
{
    private readonly AppDbContext _db;
    private readonly IProductPriceHistoryService _priceHistoryService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<PriceChangeService> _logger;

    public PriceChangeService(
        AppDbContext db,
        IProductPriceHistoryService priceHistoryService,
        IAuditLogService auditLogService,
        ILogger<PriceChangeService> logger)
    {
        _db = db;
        _priceHistoryService = priceHistoryService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<PriceChangeResult> ChangePriceAsync(
        PriceChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidatePriceChangeAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
            return PriceChangeResult.Fail(validation.ErrorMessage ?? "Price change validation failed.");

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                var product = await _db.Products
                    .FirstOrDefaultAsync(
                        p => p.Id == request.ProductId && p.TenantId == request.TenantId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (product is null)
                    return PriceChangeResult.Fail("Product not found");

                var newGroup = await _db.TaxGroups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        g => g.Id == request.NewTaxGroupId
                             && g.TenantId == request.TenantId
                             && g.IsActive,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (newGroup is null)
                    return PriceChangeResult.Fail("Tax group not found or inactive.");

                var oldPrice = decimal.Round(product.Price, 2, MidpointRounding.AwayFromZero);
                var newPrice = decimal.Round(request.NewPrice, 2, MidpointRounding.AwayFromZero);
                var oldTaxGroupId = product.TaxGroupId;
                var oldTaxRate = decimal.Round(product.TaxRate, 2, MidpointRounding.AwayFromZero);
                var newTaxRate = decimal.Round(newGroup.Rate, 2, MidpointRounding.AwayFromZero);

                if (oldPrice == newPrice && oldTaxGroupId == request.NewTaxGroupId && oldTaxRate == newTaxRate)
                    return PriceChangeResult.Fail("Price and tax group are unchanged.");

                product.Price = newPrice;
                product.TaxGroupId = request.NewTaxGroupId;
                product.TaxRate = newTaxRate;
                product.TaxType = TaxTypes.FromTaxGroup(newGroup);
                product.UpdatedAt = DateTime.UtcNow;

                if (oldTaxGroupId != request.NewTaxGroupId || oldTaxRate != newTaxRate)
                {
                    _db.TaxHistories.Add(new TaxHistory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = request.TenantId,
                        ProductId = product.Id,
                        TaxGroupId = request.NewTaxGroupId,
                        OldRate = oldTaxRate,
                        NewRate = newTaxRate,
                        ChangedAt = DateTime.UtcNow,
                        ChangedBy = request.ChangedBy == Guid.Empty ? Guid.Empty : request.ChangedBy,
                        Reason = string.IsNullOrWhiteSpace(request.Reason)
                            ? "Product tax group updated"
                            : request.Reason.Trim(),
                    });
                }

                var history = await _priceHistoryService.RecordChangeAsync(
                    request.TenantId,
                    product.Id,
                    oldPrice,
                    newPrice,
                    oldTaxGroupId,
                    request.NewTaxGroupId,
                    oldTaxRate,
                    newTaxRate,
                    request.ChangedBy,
                    reason: string.IsNullOrWhiteSpace(request.Reason)
                        ? "Product price updated"
                        : request.Reason.Trim(),
                    isRksvCompliant: true,
                    rksvNote: validation.HasFiscalHistory
                        ? "Price change logged for RKSV compliance (product has prior sales history)."
                        : "Price change logged for RKSV compliance",
                    saveChanges: false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                var currentVersion = await _db.ProductPriceVersions
                    .AsNoTracking()
                    .Where(v => v.TenantId == request.TenantId && v.ProductId == product.Id && v.IsCurrent)
                    .OrderByDescending(v => v.ValidFrom)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    var actorId = request.ChangedBy == Guid.Empty
                        ? "system"
                        : request.ChangedBy.ToString("D");
                    var actorRole = string.IsNullOrWhiteSpace(request.ChangedByRole)
                        ? "Manager"
                        : request.ChangedByRole.Trim();

                    await _auditLogService.LogSystemOperationAsync(
                        action: "PRODUCT_PRICE_CHANGED",
                        entityType: "Product",
                        userId: actorId,
                        userRole: actorRole,
                        description: "Product price and/or tax group changed",
                        notes: request.Reason,
                        actionType: AuditEventType.ProductPriceChanged,
                        entityId: product.Id,
                        tenantId: product.TenantId,
                        oldValues: new
                        {
                            Price = oldPrice,
                            TaxGroupId = oldTaxGroupId,
                            TaxRate = oldTaxRate,
                        },
                        newValues: new
                        {
                            Price = newPrice,
                            TaxGroupId = request.NewTaxGroupId,
                            TaxRate = newTaxRate,
                            Reason = request.Reason,
                            PriceVersionId = currentVersion?.Id,
                            Version = currentVersion?.Version,
                            HistoryId = history?.Id,
                        },
                        entityName: product.Name).ConfigureAwait(false);
                }
                catch (Exception auditEx)
                {
                    _logger.LogWarning(
                        auditEx,
                        "Audit log failed after price change for product {ProductId}",
                        product.Id);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "RKSV price change for product {ProductId}: {OldPrice} → {NewPrice}, tax {OldRate}% → {NewRate}% (tenant {TenantId})",
                    product.Id,
                    oldPrice,
                    newPrice,
                    oldTaxRate,
                    newTaxRate,
                    request.TenantId);

                return PriceChangeResult.Success(
                    product.Id,
                    currentVersion?.Id ?? Guid.Empty,
                    currentVersion?.Version,
                    oldPrice,
                    newPrice,
                    oldTaxGroupId,
                    request.NewTaxGroupId,
                    oldTaxRate,
                    newTaxRate,
                    validation.WarningMessage);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError(ex, "Price change failed for product {ProductId}", request.ProductId);
                return PriceChangeResult.Fail($"Price change failed: {ex.Message}");
            }
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProductPriceHistory>> GetPriceHistoryAsync(
        Guid tenantId,
        Guid productId,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (productId == Guid.Empty)
            throw new ArgumentException("Product id is required.", nameof(productId));

        var limit = Math.Clamp(take, 1, 500);
        return await _db.ProductPriceHistories
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.ProductId == productId)
            .OrderByDescending(h => h.EffectiveFrom)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PriceChangeValidationResult> ValidatePriceChangeAsync(
        PriceChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return PriceChangeValidationResult.Fail("Request is required.");
        if (request.TenantId == Guid.Empty)
            return PriceChangeValidationResult.Fail("Tenant id is required.");
        if (request.ProductId == Guid.Empty)
            return PriceChangeValidationResult.Fail("Product id is required.");
        if (request.NewTaxGroupId == Guid.Empty)
            return PriceChangeValidationResult.Fail("New tax group id is required.");
        if (request.NewPrice < 0)
            return PriceChangeValidationResult.Fail("Price cannot be negative.");
        if (request.NewPrice == 0)
            return PriceChangeValidationResult.Fail("Price must be greater than zero.");

        var productExists = await _db.Products
            .AsNoTracking()
            .AnyAsync(p => p.Id == request.ProductId && p.TenantId == request.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!productExists)
            return PriceChangeValidationResult.Fail("Product not found");

        var taxGroupExists = await _db.TaxGroups
            .AsNoTracking()
            .AnyAsync(
                g => g.Id == request.NewTaxGroupId && g.TenantId == request.TenantId && g.IsActive,
                cancellationToken)
            .ConfigureAwait(false);
        if (!taxGroupExists)
            return PriceChangeValidationResult.Fail("Tax group not found or inactive.");

        // Order lines retain sold unit prices; catalog change must version, not rewrite fiscal history.
        var hasFiscalHistory = await _db.OrderItems
            .AsNoTracking()
            .AnyAsync(oi => oi.ProductId == request.ProductId, cancellationToken)
            .ConfigureAwait(false);

        if (hasFiscalHistory)
        {
            return PriceChangeValidationResult.Warn(
                "Product has existing sales history. " +
                "A new price version will be created to maintain RKSV compliance.",
                hasFiscalHistory: true);
        }

        return PriceChangeValidationResult.Success();
    }
}
