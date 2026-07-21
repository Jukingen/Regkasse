import type { ProductFilters } from '@/features/products/types/productFilters';

/** Counts distinct filter dimensions (not individual multi-select values). */
export function countActiveProductFilters(filters: ProductFilters): number {
  let count = 0;
  if (filters.searchTerm?.trim()) count++;
  if (filters.minPrice != null && Number.isFinite(filters.minPrice)) count++;
  if (filters.maxPrice != null && Number.isFinite(filters.maxPrice)) count++;
  if (filters.stockStatus && filters.stockStatus !== 'All') count++;
  if (filters.minStock != null && Number.isFinite(filters.minStock)) count++;
  if (filters.maxStock != null && Number.isFinite(filters.maxStock)) count++;
  if (filters.taxTypes && filters.taxTypes.length > 0) count++;
  if (filters.categoryIds && filters.categoryIds.length > 0) count++;
  if (filters.status && filters.status !== 'active') count++;
  if (filters.isTaxable != null) count++;
  if (filters.createdRange?.[0] && filters.createdRange[1]) count++;
  return count;
}
