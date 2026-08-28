import { describe, expect, it } from 'vitest';

import {
  buildProductListSearchParams,
  parseProductFiltersFromSearchParams,
} from '../productFilterUrl';
import {
  findCategoryByName,
  resolveProductCategoryIds,
  resolveShareableCategoryName,
} from '../resolveProductCategoryFilter';

const CATEGORIES = [
  { id: 'cat-kebab', name: 'Kebab', sortOrder: 1 },
  { id: 'cat-drinks', name: 'Getränke', sortOrder: 2 },
];

describe('resolveProductCategoryFilter', () => {
  it('matches category names case-insensitively', () => {
    expect(findCategoryByName(CATEGORIES, 'kebab')?.id).toBe('cat-kebab');
    expect(findCategoryByName(CATEGORIES, '  Kebab ' )?.name).toBe('Kebab');
  });

  it('resolves shareable ?category=Name to a single id', () => {
    expect(resolveProductCategoryIds({ categoryName: 'Kebab' }, CATEGORIES)).toEqual(['cat-kebab']);
  });

  it('prefers explicit categoryIds over the shareable name', () => {
    expect(
      resolveProductCategoryIds(
        { categoryName: 'Kebab', categoryIds: ['cat-drinks'] },
        CATEGORIES
      )
    ).toEqual(['cat-drinks']);
  });

  it('passes through a UUID category token before the list is loaded', () => {
    const id = '11111111-1111-4111-8111-111111111111';
    expect(resolveProductCategoryIds({ categoryName: id }, [])).toEqual([id]);
  });
});

describe('productFilterUrl category param', () => {
  it('parses ?category=Kebab into categoryName', () => {
    const filters = parseProductFiltersFromSearchParams(new URLSearchParams('category=Kebab'));
    expect(filters.categoryName).toBe('Kebab');
    expect(filters.categoryIds).toBeUndefined();
  });

  it('writes a shareable category name for a single selected category', () => {
    const params = buildProductListSearchParams(
      { categoryIds: ['cat-kebab'], categoryName: 'Kebab', status: 'active' },
      { page: 1, pageSize: 10 },
      new URLSearchParams(),
      CATEGORIES
    );
    expect(params.get('category')).toBe('Kebab');
    expect(params.get('categoryIds')).toBeNull();
  });

  it('keeps categoryIds in the URL when multiple categories are selected', () => {
    const params = buildProductListSearchParams(
      { categoryIds: ['cat-kebab', 'cat-drinks'], status: 'active' },
      { page: 1, pageSize: 10 },
      new URLSearchParams(),
      CATEGORIES
    );
    expect(params.get('category')).toBeNull();
    expect(params.get('categoryIds')).toBe('cat-kebab,cat-drinks');
  });

  it('round-trips a shareable category name', () => {
    const built = buildProductListSearchParams(
      { categoryName: 'Getränke', status: 'active' },
      { page: 1, pageSize: 10 },
      new URLSearchParams(),
      CATEGORIES
    );
    const parsed = parseProductFiltersFromSearchParams(built);
    expect(parsed.categoryName).toBe('Getränke');
    expect(resolveShareableCategoryName(parsed, CATEGORIES)).toBe('Getränke');
    expect(resolveProductCategoryIds(parsed, CATEGORIES)).toEqual(['cat-drinks']);
  });
});
