import type { Dayjs } from 'dayjs';

export type ProductStockFilterType = 'InStock' | 'OutOfStock' | 'LowStock' | 'Overstock' | 'All';

export type ProductListActiveFilter = 'active' | 'inactive' | 'all';

/** Client-side product list filters (mapped to GET /api/admin/products query params). */
export interface ProductFilters {
  searchTerm?: string;
  searchInName?: boolean;
  searchInDescription?: boolean;
  searchInBarcode?: boolean;
  minPrice?: number;
  maxPrice?: number;
  stockStatus?: ProductStockFilterType;
  minStock?: number;
  maxStock?: number;
  taxTypes?: number[];
  categoryIds?: string[];
  status?: ProductListActiveFilter;
  isTaxable?: boolean;
  createdRange?: [Dayjs, Dayjs] | null;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface ProductFilterSummary {
  activeFilterCount?: number;
  appliedFilters?: Record<string, unknown>;
  availableTaxTypes?: number[];
}
