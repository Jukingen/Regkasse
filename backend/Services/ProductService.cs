using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public interface IProductService
{
    /// <summary>Active products for a tenant; optional category filter. Read-only projection (no tracking).</summary>
    Task<List<ProductListDto>> GetProductsAsync(
        Guid tenantId,
        Guid? categoryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Paged active products for a tenant. Read-only projection (no tracking).</summary>
    Task<PagedResult<ProductListDto>> GetProductsPagedAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 20,
        Guid? categoryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops cached product list entries for a tenant (and optional single-product key).
    /// Call after create / update / delete / stock changes — writes live on admin/product controllers.
    /// </summary>
    Task InvalidateProductsCacheAsync(Guid tenantId, Guid? productId = null);

    /// <summary>
    /// Trims and validates an optional product barcode. Empty becomes null.
    /// Duplicate within the ambient tenant returns <see cref="ProductBarcodeWriteStatus.Duplicate"/>.
    /// </summary>
    Task<ProductBarcodeWriteResult> PrepareBarcodeAsync(
        Guid tenantId,
        string? rawBarcode,
        Guid? exceptProductId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Active product for the ambient tenant, or null when the barcode is unknown.</summary>
    Task<Product?> GetByBarcodeAsync(
        Guid tenantId,
        string barcode,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Optimized product read queries: AsNoTracking + server-side <see cref="ProductListDto"/> projection + pagination.
/// Does not replace admin list filters (<see cref="IAdminProductListService"/>) or POS catalog.
/// </summary>
public sealed class ProductService : IProductService
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;
    private readonly CacheSettings _cacheSettings;

    public ProductService(AppDbContext db, ICacheService cache, IOptions<CacheSettings> cacheSettings)
    {
        _db = db;
        _cache = cache;
        _cacheSettings = cacheSettings.Value;
    }

    public async Task<List<ProductListDto>> GetProductsAsync(
        Guid tenantId,
        Guid? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildProductsListCacheKey(tenantId, categoryId);

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await ProjectToListDto(BuildActiveProductsQuery(tenantId, categoryId))
                .OrderBy(p => p.CategoryName)
                .ThenBy(p => p.Name)
                .ToListAsync(ct),
            _cacheSettings.ProductCacheTtl,
            cancellationToken);
    }

    public async Task<PagedResult<ProductListDto>> GetProductsPagedAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 20,
        Guid? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = BuildActiveProductsQuery(tenantId, categoryId);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await ProjectToListDto(query)
            .OrderBy(p => p.CategoryName)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductListDto>
        {
            Items = items,
            Total = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public Task InvalidateProductsCacheAsync(Guid tenantId, Guid? productId = null) =>
        CacheInvalidationHelper.InvalidateProductCacheAsync(_cache, tenantId, productId);

    private IQueryable<Product> BuildActiveProductsQuery(Guid tenantId, Guid? categoryId)
    {
        var query = _db.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive);

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        return query;
    }

    /// <summary>
    /// Server-side projection: only list fields are selected (no full <see cref="Product"/> entity materialization).
    /// Category is a string column on Product; display name prefers <see cref="Product.CategoryNavigation"/>.
    /// </summary>
    private static IQueryable<ProductListDto> ProjectToListDto(IQueryable<Product> query) =>
        query.Select(p => new ProductListDto
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.Price,
            CategoryId = p.CategoryId,
            CategoryName = p.CategoryNavigation != null ? p.CategoryNavigation.Name : p.Category,
            IsActive = p.IsActive,
            Barcode = p.Barcode,
            ImageUrl = p.ImageUrl,
            CreatedAt = p.CreatedAt,
        });

    public async Task<ProductBarcodeWriteResult> PrepareBarcodeAsync(
        Guid tenantId,
        string? rawBarcode,
        Guid? exceptProductId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = ProductBarcodeRules.Normalize(rawBarcode);
        if (normalized is null)
            return ProductBarcodeWriteResult.Ok(null);

        if (!ProductBarcodeRules.IsValid(normalized))
        {
            return ProductBarcodeWriteResult.Invalid(
                "Barcode must be 1-64 letters, digits, or hyphens.");
        }

        var taken = await _db.Products.AsNoTracking().AnyAsync(
            product => product.TenantId == tenantId
                && product.Barcode == normalized
                && (exceptProductId == null || product.Id != exceptProductId),
            cancellationToken);
        if (taken)
            return ProductBarcodeWriteResult.Duplicate();

        return ProductBarcodeWriteResult.Ok(normalized);
    }

    public async Task<Product?> GetByBarcodeAsync(
        Guid tenantId,
        string barcode,
        CancellationToken cancellationToken = default)
    {
        var normalized = ProductBarcodeRules.Normalize(barcode);
        if (normalized is null || !ProductBarcodeRules.IsValid(normalized))
            return null;

        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(
            item => item.TenantId == tenantId && item.IsActive && item.Barcode == normalized,
            cancellationToken);
        if (product is null)
            return null;

        product.HasVariants = await _db.ProductVariants.AsNoTracking().AnyAsync(
            variant => variant.ProductId == product.Id,
            cancellationToken);
        return product;
    }

    private static string BuildProductsListCacheKey(Guid tenantId, Guid? categoryId) =>
        categoryId is { } cat
            ? CacheKeys.Format(CacheKeys.ProductListByCategory, tenantId, cat)
            : CacheKeys.Format(CacheKeys.ProductList, tenantId);
}

public enum ProductBarcodeWriteStatus
{
    Ok,
    Invalid,
    Duplicate,
}

public sealed record ProductBarcodeWriteResult(ProductBarcodeWriteStatus Status, string? Barcode, string? Message)
{
    public const string DuplicateCode = "BARCODE_DUPLICATE";
    public const string NotFoundCode = "BARCODE_NOT_FOUND";

    public static ProductBarcodeWriteResult Ok(string? barcode) =>
        new(ProductBarcodeWriteStatus.Ok, barcode, null);

    public static ProductBarcodeWriteResult Invalid(string message) =>
        new(ProductBarcodeWriteStatus.Invalid, null, message);

    public static ProductBarcodeWriteResult Duplicate() =>
        new(
            ProductBarcodeWriteStatus.Duplicate,
            null,
            "A product with this barcode already exists for this tenant.");
}

public static class ProductBarcodeRules
{
    public const int MaxLength = 64;

    private static readonly Regex Pattern = new(
        "^[A-Za-z0-9-]{1,64}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return raw.Trim();
    }

    public static bool IsValid(string barcode) => Pattern.IsMatch(barcode);
}
