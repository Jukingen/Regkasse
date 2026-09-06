import type { ProductFilters } from '@/features/products/types/productFilters';

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export type CategoryFilterOption = {
  id?: string;
  name?: string | null;
  key?: string | null;
  color?: string | null;
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
  return findCategoryByToken(categories, name);
}

export function findCategoryById<T extends CategoryFilterOption>(
  categories: T[],
  id: string
): T | undefined {
  const needle = id.trim().toLowerCase();
  if (!needle) return undefined;
  return categories.find((category) => category.id?.toLowerCase() === needle);
}

/** Matches a category by id, display name, or slug key (`döner-box` → Kebab). */
export function findCategoryByToken<T extends CategoryFilterOption>(
  categories: T[],
  token: string | null | undefined
): T | undefined {
  const raw = token?.trim();
  if (!raw) return undefined;
  if (isCategoryFilterUuid(raw)) {
    return findCategoryById(categories, raw);
  }
  const needle = normalizeCategoryFilterName(raw);
  return categories.find((category) => {
    if (!category.id) return false;
    if (category.name && normalizeCategoryFilterName(category.name) === needle) return true;
    if (category.key && normalizeCategoryFilterName(category.key) === needle) return true;
    return false;
  });
}

/** Catalog first (name/key/color), then list availableFilters as fallback. */
export function mergeCategoriesForProductFilter<T extends CategoryFilterOption>(
  catalog: T[],
  available?: Array<{ id?: string; name?: string | null }> | null
): T[] {
  const byId = new Map<string, T>();
  for (const category of catalog) {
    if (category.id) byId.set(category.id, category);
  }
  for (const extra of available ?? []) {
    const id = extra.id?.trim();
    if (!id || byId.has(id)) continue;
    byId.set(id, {
      id,
      key: id,
      name: extra.name ?? '',
    } as T);
  }
  return sortCategoriesForProductFilter([...byId.values()]);
}

export function resolveProductCategoryDisplay(
  product: { categoryId?: string | null; category?: string | null },
  categories: CategoryFilterOption[]
): { id?: string; name: string; color?: string | null } {
  const match =
    findCategoryByToken(categories, product.categoryId) ??
    findCategoryByToken(categories, product.category);
  return {
    id: match?.id ?? product.categoryId ?? undefined,
    name: match?.name?.trim() || product.category?.trim() || '',
    color: match?.color,
  };
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

  const match = findCategoryByToken(categories, token);
  if (match?.id) return [match.id];
  if (isCategoryFilterUuid(token)) return [token];
  return undefined;
}

export function resolveShareableCategoryName(
  filters: ProductFilters,
  categories: CategoryFilterOption[]
): string | undefined {
  const ids = resolveProductCategoryIds(filters, categories);
  if (ids && ids.length !== 1) {
    return undefined;
  }

  if (ids?.length === 1) {
    const fromList = findCategoryById(categories, ids[0])?.name?.trim();
    if (fromList) return fromList;
  }

  const named = filters.categoryName?.trim();
  if (named && !isCategoryFilterUuid(named)) {
    const fromToken = findCategoryByToken(categories, named)?.name?.trim();
    return fromToken || named;
  }

  return named || undefined;
}
