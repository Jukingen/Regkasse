using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.Cache;
using Microsoft.EntityFrameworkCore;

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
}

/// <summary>
/// Optimized product read queries: AsNoTracking + server-side <see cref="ProductListDto"/> projection + pagination.
/// Does not replace admin list filters (<see cref="IAdminProductListService"/>) or POS catalog.
/// </summary>
public sealed class ProductService : IProductService
{
    private static readonly TimeSpan ProductsListCacheExpiry = TimeSpan.FromMinutes(10);

    private readonly AppDbContext _db;
    private readonly ICacheService _cache;

    public ProductService(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<List<ProductListDto>> GetProductsAsync(
        Guid tenantId,
        Guid? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildProductsListCacheKey(tenantId, categoryId);

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async () => await ProjectToListDto(BuildActiveProductsQuery(tenantId, categoryId))
                .OrderBy(p => p.CategoryName)
                .ThenBy(p => p.Name)
                .ToListAsync(cancellationToken),
            ProductsListCacheExpiry);
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

    public async Task InvalidateProductsCacheAsync(Guid tenantId, Guid? productId = null)
    {
        // Prefix covers unfiltered + category-filtered list keys for this tenant.
        await _cache.RemoveByPrefixAsync(ProductsCachePrefix(tenantId));

        if (productId is { } id)
            await _cache.RemoveAsync($"product_{id}");
    }

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

    private static string ProductsCachePrefix(Guid tenantId) => $"products_{tenantId}";

    private static string BuildProductsListCacheKey(Guid tenantId, Guid? categoryId) =>
        categoryId is { } cat
            ? $"{ProductsCachePrefix(tenantId)}_cat_{cat}"
            : ProductsCachePrefix(tenantId);
}
