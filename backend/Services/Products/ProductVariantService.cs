using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Products;

public sealed class ProductVariantService : IProductVariantService
{
    public const decimal MaxPriceDelta = 999_999.99m;

    private readonly AppDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly ILogger<ProductVariantService> _logger;

    public ProductVariantService(
        AppDbContext db,
        IAuditLogService audit,
        ILogger<ProductVariantService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProductVariantDto>?> ListAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (!await ProductExistsAsync(productId, cancellationToken).ConfigureAwait(false))
            return null;

        var rows = await _db.ProductVariants
            .AsNoTracking()
            .Where(v => v.ProductId == productId)
            .OrderBy(v => v.VariantLabel)
            .ThenBy(v => v.Sku)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToDto).ToList();
    }

    public async Task<ProductVariantMutation> CreateAsync(
        Guid productId,
        CreateProductVariantRequest request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var product = await FindProductAsync(productId, cancellationToken).ConfigureAwait(false);
        if (product == null)
            return new ProductVariantMutation.NotFound();

        var sku = NormalizeSku(request.Sku);
        var label = NormalizeLabel(request.VariantLabel);
        var invalid = ValidateFields(sku, label, request.PriceDelta, request.Stock);
        if (invalid != null)
            return invalid;

        var duplicate = await FindDuplicateAsync(product.TenantId, product.Id, sku, label, null, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate != null)
            return duplicate;

        var now = DateTime.UtcNow;
        var row = new ProductVariant
        {
            Id = Guid.NewGuid(),
            TenantId = product.TenantId,
            ProductId = product.Id,
            Sku = sku,
            VariantLabel = label,
            PriceDelta = request.PriceDelta,
            Stock = request.Stock,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        _db.ProductVariants.Add(row);

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Product variant create conflict for product {ProductId}", productId);
            return DuplicateFromException(ex);
        }

        await WriteChangedAuditAsync(
            product.TenantId,
            product.Id,
            row.Id,
            actorUserId,
            changedFields: ["sku", "variantLabel", "priceDelta", "stock", "isActive"],
            oldValues: null,
            row,
            cancellationToken).ConfigureAwait(false);

        return new ProductVariantMutation.Ok(ToDto(row));
    }

    public async Task<ProductVariantMutation> UpdateAsync(
        Guid productId,
        Guid variantId,
        UpdateProductVariantRequest request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var product = await FindProductAsync(productId, cancellationToken).ConfigureAwait(false);
        if (product == null)
            return new ProductVariantMutation.NotFound();

        var row = await FindVariantAsync(product.Id, variantId, cancellationToken).ConfigureAwait(false);
        if (row == null)
            return new ProductVariantMutation.NotFound();

        var sku = NormalizeSku(request.Sku);
        var label = NormalizeLabel(request.VariantLabel);
        var invalid = ValidateFields(sku, label, request.PriceDelta, request.Stock);
        if (invalid != null)
            return invalid;

        var duplicate = await FindDuplicateAsync(product.TenantId, product.Id, sku, label, row.Id, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate != null)
            return duplicate;

        var changed = new List<string>();
        if (!string.Equals(row.Sku, sku, StringComparison.Ordinal))
            changed.Add("sku");
        if (!string.Equals(row.VariantLabel, label, StringComparison.Ordinal))
            changed.Add("variantLabel");
        if (row.PriceDelta != request.PriceDelta)
            changed.Add("priceDelta");
        if (row.Stock != request.Stock)
            changed.Add("stock");
        if (row.IsActive != request.IsActive)
            changed.Add("isActive");

        var previous = ToAuditValues(row);
        row.Sku = sku;
        row.VariantLabel = label;
        row.PriceDelta = request.PriceDelta;
        row.Stock = request.Stock;
        row.IsActive = request.IsActive;
        row.UpdatedAtUtc = DateTime.UtcNow;

        if (changed.Count == 0)
            return new ProductVariantMutation.Ok(ToDto(row));

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Product variant update conflict for {VariantId}", variantId);
            return DuplicateFromException(ex);
        }

        await WriteChangedAuditAsync(
            product.TenantId,
            product.Id,
            row.Id,
            actorUserId,
            changed,
            previous,
            row,
            cancellationToken).ConfigureAwait(false);

        return new ProductVariantMutation.Ok(ToDto(row));
    }

    public async Task<ProductVariantMutation> DeactivateAsync(
        Guid productId,
        Guid variantId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var product = await FindProductAsync(productId, cancellationToken).ConfigureAwait(false);
        if (product == null)
            return new ProductVariantMutation.NotFound();

        var row = await FindVariantAsync(product.Id, variantId, cancellationToken).ConfigureAwait(false);
        if (row == null)
            return new ProductVariantMutation.NotFound();

        if (!row.IsActive)
            return new ProductVariantMutation.Ok(ToDto(row));

        var previous = ToAuditValues(row);
        row.IsActive = false;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await WriteChangedAuditAsync(
            product.TenantId,
            product.Id,
            row.Id,
            actorUserId,
            changedFields: ["isActive"],
            previous,
            row,
            cancellationToken).ConfigureAwait(false);

        return new ProductVariantMutation.Ok(ToDto(row));
    }

    public async Task<ProductVariantMutation> AdjustStockAsync(
        Guid productId,
        Guid variantId,
        int delta,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var product = await FindProductAsync(productId, cancellationToken).ConfigureAwait(false);
        if (product == null)
            return new ProductVariantMutation.NotFound();

        var row = await FindVariantAsync(product.Id, variantId, cancellationToken).ConfigureAwait(false);
        if (row == null)
            return new ProductVariantMutation.NotFound();

        int next;
        try
        {
            next = checked(row.Stock + delta);
        }
        catch (OverflowException)
        {
            return Invalid("Stock delta is out of range.");
        }

        if (next < 0)
            return Invalid("Variant stock cannot be negative.");

        row.Stock = next;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _audit.LogSystemOperationAsync(
                action: "PRODUCT_VARIANT_STOCK_ADJUSTED",
                entityType: "ProductVariant",
                userId: Actor(actorUserId),
                userRole: "Manager",
                description: "PRODUCT_VARIANT_STOCK_ADJUSTED",
                actionType: AuditEventType.ProductVariantStockAdjusted,
                entityId: row.Id,
                tenantId: product.TenantId,
                newValues: new
                {
                    tenantId = product.TenantId,
                    productId = product.Id,
                    variantId = row.Id,
                    delta,
                    newStock = row.Stock,
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product variant stock audit failed for {VariantId}", row.Id);
        }

        return new ProductVariantMutation.Ok(ToDto(row));
    }

    private async Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken) =>
        await _db.Products.AsNoTracking().AnyAsync(p => p.Id == productId, cancellationToken).ConfigureAwait(false);

    private Task<Product?> FindProductAsync(Guid productId, CancellationToken cancellationToken) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

    private Task<ProductVariant?> FindVariantAsync(
        Guid productId,
        Guid variantId,
        CancellationToken cancellationToken) =>
        _db.ProductVariants.FirstOrDefaultAsync(
            v => v.Id == variantId && v.ProductId == productId,
            cancellationToken);

    private async Task<ProductVariantMutation?> FindDuplicateAsync(
        Guid tenantId,
        Guid productId,
        string sku,
        string label,
        Guid? exceptVariantId,
        CancellationToken cancellationToken)
    {
        var skuLower = sku.ToLowerInvariant();
        var labelLower = label.ToLowerInvariant();

        var skuTaken = await _db.ProductVariants.AsNoTracking().AnyAsync(
            v => v.TenantId == tenantId
                 && v.Sku.ToLower() == skuLower
                 && (exceptVariantId == null || v.Id != exceptVariantId),
            cancellationToken).ConfigureAwait(false);
        if (skuTaken)
        {
            return new ProductVariantMutation.Error(
                StatusCodes.Status409Conflict,
                "SKU_DUPLICATE",
                "SKU already exists for this tenant.");
        }

        var labelTaken = await _db.ProductVariants.AsNoTracking().AnyAsync(
            v => v.ProductId == productId
                 && v.VariantLabel.ToLower() == labelLower
                 && (exceptVariantId == null || v.Id != exceptVariantId),
            cancellationToken).ConfigureAwait(false);
        if (labelTaken)
        {
            return new ProductVariantMutation.Error(
                StatusCodes.Status409Conflict,
                "VARIANT_LABEL_DUPLICATE",
                "Variant label already exists for this product.");
        }

        return null;
    }

    private static ProductVariantMutation? ValidateFields(string sku, string label, decimal priceDelta, int stock)
    {
        if (sku.Length is 0 or > 64 || label.Length is 0 or > 128)
            return Invalid("SKU and variant label are required.");
        if (stock < 0)
            return Invalid("Variant stock cannot be negative.");
        if (priceDelta < -MaxPriceDelta || priceDelta > MaxPriceDelta || decimal.Round(priceDelta, 2) != priceDelta)
            return Invalid("Price delta is out of range.");
        return null;
    }

    private static ProductVariantMutation.Error Invalid(string message) =>
        new(StatusCodes.Status400BadRequest, "VARIANT_INVALID", message);

    private static ProductVariantMutation.Error DuplicateFromException(DbUpdateException ex)
    {
        var text = ex.InnerException?.Message ?? ex.Message;
        if (text.Contains("variant_label", StringComparison.OrdinalIgnoreCase))
        {
            return new ProductVariantMutation.Error(
                StatusCodes.Status409Conflict,
                "VARIANT_LABEL_DUPLICATE",
                "Variant label already exists for this product.");
        }

        return new ProductVariantMutation.Error(
            StatusCodes.Status409Conflict,
            "SKU_DUPLICATE",
            "SKU already exists for this tenant.");
    }

    private async Task WriteChangedAuditAsync(
        Guid tenantId,
        Guid productId,
        Guid variantId,
        string actorUserId,
        IReadOnlyList<string> changedFields,
        object? oldValues,
        ProductVariant row,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            await _audit.LogSystemOperationAsync(
                action: "PRODUCT_VARIANT_CHANGED",
                entityType: "ProductVariant",
                userId: Actor(actorUserId),
                userRole: "Manager",
                description: "PRODUCT_VARIANT_CHANGED",
                actionType: AuditEventType.ProductVariantChanged,
                entityId: variantId,
                tenantId: tenantId,
                oldValues: oldValues,
                newValues: new
                {
                    tenantId,
                    productId,
                    variantId,
                    changedFields,
                    sku = row.Sku,
                    variantLabel = row.VariantLabel,
                    priceDelta = row.PriceDelta,
                    stock = row.Stock,
                    isActive = row.IsActive,
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product variant audit failed for {VariantId}", variantId);
        }
    }

    private static object ToAuditValues(ProductVariant row) => new
    {
        sku = row.Sku,
        variantLabel = row.VariantLabel,
        priceDelta = row.PriceDelta,
        stock = row.Stock,
        isActive = row.IsActive,
    };

    private static ProductVariantDto ToDto(ProductVariant row) => new()
    {
        Id = row.Id,
        ProductId = row.ProductId,
        Sku = row.Sku,
        VariantLabel = row.VariantLabel,
        PriceDelta = row.PriceDelta,
        Stock = row.Stock,
        IsActive = row.IsActive,
    };

    private static string NormalizeSku(string? sku) => (sku ?? string.Empty).Trim();

    private static string NormalizeLabel(string? label) => (label ?? string.Empty).Trim();

    private static string Actor(string? actorUserId) =>
        string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId;
}
