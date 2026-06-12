import dayjs from 'dayjs';
import type { ProductFilters, ProductListActiveFilter } from '@/features/products/types/productFilters';

const FILTER_PARAM_KEYS = [
    'searchTerm',
    'searchInDescription',
    'searchInBarcode',
    'minPrice',
    'maxPrice',
    'stockStatus',
    'minStock',
    'maxStock',
    'taxTypes',
    'categoryIds',
    'status',
    'isTaxable',
    'createdFrom',
    'createdTo',
    'sortBy',
    'sortDirection',
    'page',
    'pageSize',
] as const;

export function createDefaultProductFilters(): ProductFilters {
    return {
        status: 'active',
        searchInName: true,
        searchInDescription: true,
        searchInBarcode: true,
    };
}

export function parseProductFiltersFromSearchParams(searchParams: URLSearchParams): ProductFilters {
    const filters: ProductFilters = createDefaultProductFilters();

    const searchTerm = searchParams.get('searchTerm') ?? searchParams.get('search');
    if (searchTerm) filters.searchTerm = searchTerm;
    if (searchParams.get('searchInDescription') === 'true') filters.searchInDescription = true;
    if (searchParams.get('searchInBarcode') === 'true') filters.searchInBarcode = true;

    const minPrice = searchParams.get('minPrice');
    if (minPrice != null && minPrice !== '' && Number.isFinite(Number(minPrice))) {
        filters.minPrice = Number(minPrice);
    }
    const maxPrice = searchParams.get('maxPrice');
    if (maxPrice != null && maxPrice !== '' && Number.isFinite(Number(maxPrice))) {
        filters.maxPrice = Number(maxPrice);
    }

    const stockStatus = searchParams.get('stockStatus');
    if (stockStatus) filters.stockStatus = stockStatus as ProductFilters['stockStatus'];

    const minStock = searchParams.get('minStock');
    if (minStock != null && minStock !== '' && Number.isFinite(Number(minStock))) {
        filters.minStock = Number(minStock);
    }
    const maxStock = searchParams.get('maxStock');
    if (maxStock != null && maxStock !== '' && Number.isFinite(Number(maxStock))) {
        filters.maxStock = Number(maxStock);
    }

    const taxTypes = searchParams.get('taxTypes');
    if (taxTypes) {
        filters.taxTypes = taxTypes
            .split(',')
            .map((v) => Number(v.trim()))
            .filter((v) => Number.isFinite(v));
    }

    const categoryIds = searchParams.get('categoryIds') ?? searchParams.get('categoryId');
    if (categoryIds) {
        filters.categoryIds = categoryIds.split(',').map((v) => v.trim()).filter(Boolean);
    }

    const status = searchParams.get('status');
    if (status === 'all' || status === 'inactive' || status === 'active') {
        filters.status = status as ProductListActiveFilter;
    }

    if (searchParams.get('isTaxable') === 'true') filters.isTaxable = true;
    if (searchParams.get('isTaxable') === 'false') filters.isTaxable = false;

    const createdFrom = searchParams.get('createdFrom');
    const createdTo = searchParams.get('createdTo');
    if (createdFrom && createdTo && dayjs(createdFrom).isValid() && dayjs(createdTo).isValid()) {
        filters.createdRange = [dayjs(createdFrom), dayjs(createdTo)];
    }

    const sortBy = searchParams.get('sortBy');
    if (sortBy) filters.sortBy = sortBy;
    const sortDirection = searchParams.get('sortDirection');
    if (sortDirection === 'asc' || sortDirection === 'desc') filters.sortDirection = sortDirection;

    return filters;
}

export function parseProductPaginationFromSearchParams(searchParams: URLSearchParams): {
    page: number;
    pageSize: number;
} {
    const pageRaw = searchParams.get('page');
    const pageSizeRaw = searchParams.get('pageSize');
    const page = pageRaw && Number.isFinite(Number(pageRaw)) ? Math.max(1, Number(pageRaw)) : 1;
    const pageSize =
        pageSizeRaw && Number.isFinite(Number(pageSizeRaw))
            ? Math.min(500, Math.max(1, Number(pageSizeRaw)))
            : 10;
    return { page, pageSize };
}

export function buildProductListSearchParams(
    filters: ProductFilters,
    pagination: { page: number; pageSize: number },
    existing: URLSearchParams,
): URLSearchParams {
    const next = new URLSearchParams(existing.toString());

    for (const key of FILTER_PARAM_KEYS) {
        next.delete(key);
    }

    if (filters.searchTerm?.trim()) next.set('searchTerm', filters.searchTerm.trim());
    if (filters.searchInDescription) next.set('searchInDescription', 'true');
    if (filters.searchInBarcode) next.set('searchInBarcode', 'true');

    if (filters.minPrice != null && Number.isFinite(filters.minPrice)) {
        next.set('minPrice', String(filters.minPrice));
    }
    if (filters.maxPrice != null && Number.isFinite(filters.maxPrice)) {
        next.set('maxPrice', String(filters.maxPrice));
    }

    if (filters.stockStatus && filters.stockStatus !== 'All') {
        next.set('stockStatus', filters.stockStatus);
    }
    if (filters.minStock != null && Number.isFinite(filters.minStock)) {
        next.set('minStock', String(filters.minStock));
    }
    if (filters.maxStock != null && Number.isFinite(filters.maxStock)) {
        next.set('maxStock', String(filters.maxStock));
    }

    if (filters.taxTypes && filters.taxTypes.length > 0) {
        next.set('taxTypes', filters.taxTypes.join(','));
    }
    if (filters.categoryIds && filters.categoryIds.length > 0) {
        next.set('categoryIds', filters.categoryIds.join(','));
    }

    if (filters.status) {
        next.set('status', filters.status);
    }

    if (filters.isTaxable === true) next.set('isTaxable', 'true');
    if (filters.isTaxable === false) next.set('isTaxable', 'false');

    const createdStart = filters.createdRange?.[0];
    const createdEnd = filters.createdRange?.[1];
    if (createdStart && createdEnd) {
        next.set('createdFrom', createdStart.format('YYYY-MM-DD'));
        next.set('createdTo', createdEnd.format('YYYY-MM-DD'));
    }

    if (filters.sortBy) next.set('sortBy', filters.sortBy);
    if (filters.sortDirection) next.set('sortDirection', filters.sortDirection);

    if (pagination.page > 1) next.set('page', String(pagination.page));
    if (pagination.pageSize !== 10) next.set('pageSize', String(pagination.pageSize));

    return next;
}
