using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IAdminProductListService
{
    Task<(ProductListResponse Response, string? ErrorCode, string? ErrorMessage)> QueryAsync(
        ProductFilterDto filter,
        bool defaultActiveOnly = true,
        CancellationToken cancellationToken = default);
}

public sealed class AdminProductListService : IAdminProductListService
{
    private readonly AppDbContext _context;
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public AdminProductListService(
        AppDbContext context,
        ISettingsTenantResolver settingsTenantResolver)
    {
        _context = context;
        _settingsTenantResolver = settingsTenantResolver;
    }

    public async Task<(ProductListResponse Response, string? ErrorCode, string? ErrorMessage)> QueryAsync(
        ProductFilterDto filter,
        bool defaultActiveOnly = true,
        CancellationToken cancellationToken = default)
    {
        filter = ProductQueryExtensions.Normalize(filter, defaultActiveOnly);

        if (filter.MinPrice.HasValue && filter.MaxPrice.HasValue && filter.MinPrice > filter.MaxPrice)
            return (new ProductListResponse(), "ADMIN_PRODUCTS_INVALID_PRICE_RANGE", "minPrice must be <= maxPrice");

        if (filter.MinStock.HasValue && filter.MaxStock.HasValue && filter.MinStock > filter.MaxStock)
            return (new ProductListResponse(), "ADMIN_PRODUCTS_INVALID_STOCK_RANGE", "minStock must be <= maxStock");

        if (filter.CreatedFrom.HasValue && filter.CreatedTo.HasValue && filter.CreatedFrom > filter.CreatedTo)
            return (new ProductListResponse(), "ADMIN_PRODUCTS_INVALID_DATE_RANGE", "createdFrom must be <= createdTo");

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

        if (filter.CategoryIds.Count > 0)
        {
            var validCategoryCount = await _context.Categories.AsNoTracking()
                .CountAsync(c => c.TenantId == tenantId && filter.CategoryIds.Contains(c.Id), cancellationToken);
            if (validCategoryCount != filter.CategoryIds.Count)
                return (new ProductListResponse(), "ADMIN_PRODUCTS_INVALID_CATEGORY", "One or more categories are not in the current tenant");
        }

        IQueryable<Product> query = _context.Products.AsNoTracking()
            .ApplyTenantScope(tenantId);

        query = query.ApplyActiveFilter(filter.IsActive);
        query = query.ApplySearchFilter(filter);
        query = query.ApplyPriceRangeFilter(filter);
        query = query.ApplyStockRangeFilter(filter);
        query = query.ApplyStockStatusFilter(filter);
        query = query.ApplyTaxTypeFilter(filter.TaxTypes);
        query = query.ApplyCategoryFilter(filter.CategoryIds);
        query = query.ApplyTaxableFilter(filter.IsTaxable);
        query = query.ApplyCreatedDateFilter(filter);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySorting(filter)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => new ProductListDto
            {
                Id = p.Id,
                Name = p.Name,
                NameDe = p.NameDe,
                NameEn = p.NameEn,
                NameTr = p.NameTr,
                Description = p.Description,
                DescriptionDe = p.DescriptionDe,
                DescriptionEn = p.DescriptionEn,
                DescriptionTr = p.DescriptionTr,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                MinStockLevel = p.MinStockLevel,
                MaxStockLevel = p.MaxStockLevel,
                TaxType = p.TaxType,
                TaxRate = p.TaxRate,
                CategoryId = p.CategoryId,
                CategoryName = p.CategoryNavigation != null ? p.CategoryNavigation.Name : p.Category,
                IsActive = p.IsActive,
                IsTaxable = p.IsTaxable,
                CreatedAt = p.CreatedAt,
                Barcode = p.Barcode,
                Unit = p.Unit,
                Cost = p.Cost,
                ImageUrl = p.ImageUrl,
            })
            .ToListAsync(cancellationToken);

        var availableFilters = await GetAvailableFiltersAsync(tenantId, cancellationToken);
        var activeFilters = ProductQueryExtensions.BuildFilterSummary(filter);
        activeFilters.AvailableTaxTypes = availableFilters.TaxTypes;

        return (new ProductListResponse
        {
            Items = items,
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize,
            AvailableFilters = availableFilters,
            ActiveFilters = activeFilters,
        }, null, null);
    }

    private async Task<ProductAvailableFiltersDto> GetAvailableFiltersAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var categories = await _context.Categories.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new ProductCategoryFilterOptionDto
            {
                Id = c.Id,
                Name = c.Name,
            })
            .ToListAsync(cancellationToken);

        return new ProductAvailableFiltersDto
        {
            TaxTypes = Models.TaxTypes.All.ToList(),
            Categories = categories,
        };
    }
}

public static class ProductListDtoMapper
{
    /// <summary>Maps list projection to flat admin DTO for clients expecting legacy list shape.</summary>
    public static AdminProductDto ToAdminProductDto(ProductListDto item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        NameDe = item.NameDe,
        NameEn = item.NameEn,
        NameTr = item.NameTr,
        Description = item.Description,
        DescriptionDe = item.DescriptionDe,
        DescriptionEn = item.DescriptionEn,
        DescriptionTr = item.DescriptionTr,
        Price = item.Price,
        StockQuantity = item.StockQuantity,
        MinStockLevel = item.MinStockLevel,
        TaxType = item.TaxType,
        TaxRate = item.TaxRate,
        CategoryId = item.CategoryId,
        Category = item.CategoryName,
        IsActive = item.IsActive,
        IsTaxable = item.IsTaxable,
        CreatedAt = item.CreatedAt,
        Barcode = item.Barcode,
        Unit = item.Unit,
        Cost = item.Cost,
        ImageUrl = item.ImageUrl,
    };
}

