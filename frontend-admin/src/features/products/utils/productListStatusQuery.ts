import type { AdminProductsListIsActiveParam } from '@/api/admin/products';

/** Segmented/Select values for the admin products list status filter (URL ?status= mirrors this). */
export const PRODUCT_LIST_STATUS_FILTER_VALUES = ['all', 'active', 'inactive'] as const;

export type ProductListActiveFilter = (typeof PRODUCT_LIST_STATUS_FILTER_VALUES)[number];

const STATUS_QUERY_STRING_VALUES = new Set(['active', 'inactive', 'all']);

export function activeFilterFromUrl(status: string | undefined): ProductListActiveFilter {
  const s = status?.trim().toLowerCase();
  if (s === 'all') return 'all';
  if (s === 'inactive') return 'inactive';
  if (s === 'active' || s === undefined || s === '') return 'active';
  return 'active';
}

export function listIsActiveQueryParam(
  filter: ProductListActiveFilter
): AdminProductsListIsActiveParam | undefined {
  if (filter === 'all') return 'all';
  if (filter === 'inactive') return 'false';
  return undefined;
}

/** True when URL should drop `status` (invalid or empty string). Undefined alone is valid (default active). */
export function shouldClearProductListStatusQueryParam(status: string | undefined): boolean {
  if (status === undefined) return false;
  const raw = String(status).trim().toLowerCase();
  if (raw === '') return true;
  return !STATUS_QUERY_STRING_VALUES.has(raw);
}
