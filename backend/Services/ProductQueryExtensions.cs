using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public static class ProductQueryExtensions
{
    public static ProductFilterDto Normalize(ProductFilterDto filter, bool defaultActiveOnly = true)
    {
        filter.Page = Math.Max(1, filter.Page);
        filter.PageSize = Math.Clamp(filter.PageSize, 1, 500);
        filter.SortBy = string.IsNullOrWhiteSpace(filter.SortBy) ? "Name" : filter.SortBy.Trim();
        filter.SortDirection = string.IsNullOrWhiteSpace(filter.SortDirection) ? "asc" : filter.SortDirection.Trim();
        filter.TaxTypes = filter.TaxTypes
            .Where(TaxTypes.IsValidTaxType)
            .Distinct()
            .ToList();
        filter.CategoryIds = filter.CategoryIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (defaultActiveOnly && !filter.IsActive.HasValue)
            filter.IsActive = true;

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm)
            && !filter.SearchInName
            && !filter.SearchInDescription
            && !filter.SearchInBarcode)
        {
            filter.SearchInName = true;
        }

        return filter;
    }

    public static IQueryable<Product> ApplyTenantScope(this IQueryable<Product> query, Guid tenantId) =>
        query.Where(p => p.TenantId == tenantId);

    public static IQueryable<Product> ApplyActiveFilter(
        this IQueryable<Product> query,
        bool? isActive)
    {
        if (!isActive.HasValue)
            return query;
        return isActive.Value
            ? query.Where(p => p.IsActive)
            : query.Where(p => !p.IsActive);
    }

    public static IQueryable<Product> ApplySearchFilter(
        this IQueryable<Product> query,
        ProductFilterDto filter)
    {
        if (string.IsNullOrWhiteSpace(filter.SearchTerm))
            return query;

        var term = $"%{filter.SearchTerm.Trim()}%";
        return query.Where(p =>
            (filter.SearchInName && EF.Functions.ILike(p.Name, term))
            || (filter.SearchInDescription && p.Description != null && EF.Functions.ILike(p.Description, term))
            || (filter.SearchInBarcode && EF.Functions.ILike(p.Barcode, term)));
    }

    public static IQueryable<Product> ApplyPriceRangeFilter(
        this IQueryable<Product> query,
        ProductFilterDto filter)
    {
        if (filter.MinPrice.HasValue)
            query = query.Where(p => p.Price >= filter.MinPrice.Value);
        if (filter.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= filter.MaxPrice.Value);
        return query;
    }

    public static IQueryable<Product> ApplyStockRangeFilter(
        this IQueryable<Product> query,
        ProductFilterDto filter)
    {
        if (filter.MinStock.HasValue)
            query = query.Where(p => p.StockQuantity >= filter.MinStock.Value);
        if (filter.MaxStock.HasValue)
            query = query.Where(p => p.StockQuantity <= filter.MaxStock.Value);
        return query;
    }

    public static IQueryable<Product> ApplyStockStatusFilter(
        this IQueryable<Product> query,
        ProductFilterDto filter)
    {
        if (!filter.StockStatus.HasValue || filter.StockStatus == StockFilterType.All)
            return query;

        return filter.StockStatus.Value switch
        {
            StockFilterType.InStock => query.Where(p => p.StockQuantity > 0),
            StockFilterType.OutOfStock => query.Where(p => p.StockQuantity == 0),
            StockFilterType.LowStock => query.Where(p => p.StockQuantity <= p.MinStockLevel),
            StockFilterType.Overstock => query.Where(p =>
                p.MaxStockLevel != null && p.StockQuantity > p.MaxStockLevel.Value),
            _ => query,
        };
    }

    public static IQueryable<Product> ApplyTaxTypeFilter(
        this IQueryable<Product> query,
        IReadOnlyList<int> taxTypes)
    {
        if (taxTypes.Count == 0)
            return query;
        return query.Where(p => taxTypes.Contains(p.TaxType));
    }

    public static IQueryable<Product> ApplyCategoryFilter(
        this IQueryable<Product> query,
        IReadOnlyList<Guid> categoryIds)
    {
        if (categoryIds.Count == 0)
            return query;
        return query.Where(p => categoryIds.Contains(p.CategoryId));
    }

    public static IQueryable<Product> ApplyTaxableFilter(
        this IQueryable<Product> query,
        bool? isTaxable)
    {
        if (!isTaxable.HasValue)
            return query;
        return query.Where(p => p.IsTaxable == isTaxable.Value);
    }

    public static IQueryable<Product> ApplyCreatedDateFilter(
        this IQueryable<Product> query,
        ProductFilterDto filter)
    {
        if (!filter.CreatedFrom.HasValue && !filter.CreatedTo.HasValue)
            return query;

        if (filter.CreatedFrom.HasValue && filter.CreatedTo.HasValue && filter.CreatedFrom > filter.CreatedTo)
            return query;

        if (filter.CreatedFrom.HasValue && filter.CreatedTo.HasValue)
        {
            var (fromUtc, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(
                filter.CreatedFrom.Value,
                filter.CreatedTo.Value);
            return query.Where(p => p.CreatedAt >= fromUtc && p.CreatedAt < toExclusiveUtc);
        }

        if (filter.CreatedFrom.HasValue)
        {
            var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(
                filter.CreatedFrom.Value,
                filter.CreatedFrom.Value);
            return query.Where(p => p.CreatedAt >= fromUtc);
        }

        var (_, toExclusive) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(
            filter.CreatedTo!.Value,
            filter.CreatedTo.Value);
        return query.Where(p => p.CreatedAt < toExclusive);
    }

    public static IQueryable<Product> ApplySorting(
        this IQueryable<Product> query,
        ProductFilterDto filter)
    {
        var desc = string.Equals(filter.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return filter.SortBy.ToLowerInvariant() switch
        {
            "price" => desc
                ? query.OrderByDescending(p => p.Price).ThenBy(p => p.Name)
                : query.OrderBy(p => p.Price).ThenBy(p => p.Name),
            "stock" or "stockquantity" => desc
                ? query.OrderByDescending(p => p.StockQuantity).ThenBy(p => p.Name)
                : query.OrderBy(p => p.StockQuantity).ThenBy(p => p.Name),
            "created" or "createdat" => desc
                ? query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Name)
                : query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Name),
            "category" => desc
                ? query.OrderByDescending(p => p.Category).ThenBy(p => p.Name)
                : query.OrderBy(p => p.Category).ThenBy(p => p.Name),
            "taxtype" => desc
                ? query.OrderByDescending(p => p.TaxType).ThenBy(p => p.Name)
                : query.OrderBy(p => p.TaxType).ThenBy(p => p.Name),
            _ => desc
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name),
        };
    }

    public static ProductFilterSummaryDto BuildFilterSummary(ProductFilterDto filter)
    {
        var applied = new Dictionary<string, object>();
        var count = 0;

        void Add<T>(string key, T? value) where T : class
        {
            if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
                return;
            applied[key] = value;
            count++;
        }

        void AddValue<T>(string key, T? value) where T : struct
        {
            if (!value.HasValue)
                return;
            applied[key] = value.Value;
            count++;
        }

        Add("searchTerm", filter.SearchTerm);
        if (filter.SearchInDescription || filter.SearchInBarcode)
        {
            applied["searchInName"] = filter.SearchInName;
            applied["searchInDescription"] = filter.SearchInDescription;
            applied["searchInBarcode"] = filter.SearchInBarcode;
            count++;
        }

        AddValue("minPrice", filter.MinPrice);
        AddValue("maxPrice", filter.MaxPrice);
        AddValue("minStock", filter.MinStock);
        AddValue("maxStock", filter.MaxStock);
        AddValue("stockStatus", filter.StockStatus);

        if (filter.TaxTypes.Count > 0)
        {
            applied["taxTypes"] = filter.TaxTypes;
            count++;
        }

        if (filter.CategoryIds.Count > 0)
        {
            applied["categoryIds"] = filter.CategoryIds;
            count++;
        }

        AddValue("isActive", filter.IsActive);
        AddValue("isTaxable", filter.IsTaxable);
        AddValue("createdFrom", filter.CreatedFrom);
        AddValue("createdTo", filter.CreatedTo);

        return new ProductFilterSummaryDto
        {
            ActiveFilterCount = count,
            AppliedFilters = applied,
            AvailableTaxTypes = Models.TaxTypes.All.ToList(),
        };
    }
}
