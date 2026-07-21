import type { AdminProductsListParams } from '@/api/admin/products';
import type { ProductFilters } from '@/features/products/types/productFilters';

const MIN_SEARCH_LENGTH = 2;

export function productFiltersToApiParams(
  filters: ProductFilters,
  pagination: { page: number; pageSize: number }
): AdminProductsListParams {
  const params: AdminProductsListParams = {
    page: pagination.page,
    pageNumber: pagination.page,
    pageSize: pagination.pageSize,
    sortBy: filters.sortBy ?? 'Name',
    sortDirection: filters.sortDirection ?? 'asc',
  };

  const term = filters.searchTerm?.trim();
  if (term && term.length >= MIN_SEARCH_LENGTH) {
    params.searchTerm = term;
    params.searchInName = filters.searchInName !== false;
    params.searchInDescription = filters.searchInDescription !== false;
    params.searchInBarcode = filters.searchInBarcode !== false;
  }

  if (filters.minPrice != null && Number.isFinite(filters.minPrice)) {
    params.minPrice = filters.minPrice;
  }
  if (filters.maxPrice != null && Number.isFinite(filters.maxPrice)) {
    params.maxPrice = filters.maxPrice;
  }

  if (filters.stockStatus && filters.stockStatus !== 'All') {
    params.stockStatus = filters.stockStatus;
  }
  if (filters.minStock != null && Number.isFinite(filters.minStock)) {
    params.minStock = filters.minStock;
  }
  if (filters.maxStock != null && Number.isFinite(filters.maxStock)) {
    params.maxStock = filters.maxStock;
  }

  if (filters.taxTypes && filters.taxTypes.length > 0) {
    params.taxTypes = filters.taxTypes;
  }
  if (filters.categoryIds && filters.categoryIds.length > 0) {
    params.categoryIds = filters.categoryIds;
  }

  if (filters.status === 'inactive') {
    params.isActive = 'false';
  } else if (filters.status === 'all') {
    params.isActive = 'all';
  } else {
    params.isActive = 'true';
  }

  if (filters.isTaxable === true) params.isTaxable = true;
  if (filters.isTaxable === false) params.isTaxable = false;

  const createdStart = filters.createdRange?.[0];
  const createdEnd = filters.createdRange?.[1];
  if (createdStart && createdEnd) {
    params.createdFrom = createdStart.format('YYYY-MM-DD');
    params.createdTo = createdEnd.format('YYYY-MM-DD');
  }

  return params;
}
