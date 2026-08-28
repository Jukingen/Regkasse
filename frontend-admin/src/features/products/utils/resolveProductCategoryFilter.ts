import type { ProductFilters } from '@/features/products/types/productFilters';

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export type CategoryFilterOption = {
  id?: string;
  name?: string | null;
  sortOrder?: number;
};

export function isCategoryFilterUuid(value: string): boolean {
  return UUID_RE.test(value.trim());
}

export function normalizeCategoryFilterName(name: string): string {
  return name.trim().toLocaleLowerCase('de-AT');
}

export function sortCategoriesForProductFilter<T extends CategoryFilterOption>(categories: T[]): T[] {
  return [...categories].sort((a, b) => {
    const byOrder = (a.sortOrder ?? 0) - (b.sortOrder ?? 0);
    if (byOrder !== 0) return byOrder;
    return (a.name ?? '').localeCompare(b.name ?? '', 'de');
  });
}

export function findCategoryByName<T extends CategoryFilterOption>(
  categories: T[],
  name: string
): T | undefined {
  const needle = normalizeCategoryFilterName(name);
  if (!needle) return undefined;
  return categories.find(
    (category) =>
      !!category.id &&
      !!category.name &&
      normalizeCategoryFilterName(category.name) === needle
  );
}

export function findCategoryById<T extends CategoryFilterOption>(
  categories: T[],
  id: string
): T | undefined {
  const needle = id.trim().toLowerCase();
  if (!needle) return undefined;
  return categories.find((category) => category.id?.toLowerCase() === needle);
}

/** Resolves UI filters to category GUIDs for GET /api/admin/products. */
export function resolveProductCategoryIds(
  filters: ProductFilters,
  categories: CategoryFilterOption[]
): string[] | undefined {
  if (filters.categoryIds && filters.categoryIds.length > 0) {
    return filters.categoryIds;
  }

  const token = filters.categoryName?.trim();
  if (!token) return undefined;

  if (isCategoryFilterUuid(token)) {
    const match = findCategoryById(categories, token);
    return [match?.id ?? token];
  }

  const byName = findCategoryByName(categories, token);
  return byName?.id ? [byName.id] : undefined;
}

export function resolveShareableCategoryName(
  filters: ProductFilters,
  categories: CategoryFilterOption[]
): string | undefined {
  const ids = resolveProductCategoryIds(filters, categories);
  if (ids && ids.length !== 1) {
    return undefined;
  }

  const named = filters.categoryName?.trim();
  if (named && !isCategoryFilterUuid(named)) {
    return named;
  }

  if (ids?.length === 1) {
    const fromList = findCategoryById(categories, ids[0])?.name?.trim();
    if (fromList) return fromList;
  }

  return named || undefined;
}
