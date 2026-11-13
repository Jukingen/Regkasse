import { describe, expect, it } from 'vitest';

import type { DemoImportCatalog } from '@/api/admin/products';
import {
  CATEGORY_GROUPS,
  toLegacyImportCategoryName,
} from '@/features/tenants/components/demo-import/categoryGroups';
import {
  type CatalogProduct,
  buildImportRequest,
  getGroupProductIds,
  getProductsForGroup,
  getSelectedCategoryNames,
} from '@/features/tenants/components/demo-import/utils';

describe('CATEGORY_GROUPS', () => {
  it('uses unique top-level group keys', () => {
    const names = CATEGORY_GROUPS.map((group) => group.name);
    expect(new Set(names).size).toBe(names.length);
  });

  it('maps Pizzen display group to the pizzas key', () => {
    const pizzas = CATEGORY_GROUPS.find((group) => group.name === 'pizzas');
    expect(pizzas).toBeDefined();
    expect(pizzas?.displayName).toContain('Pizzen');
  });
});

const normalizedCatalogProducts: CatalogProduct[] = [
  { id: 'p1', name: 'Margherita', category: 'Pizza, mittel', price: 9.8, taxRate: 10 },
  { id: 'p2', name: 'Partner Pizza', category: 'Pizza, Partner', price: 12.5, taxRate: 10 },
  { id: 'd1', name: 'Cola', category: 'Alkoholfreie Getränke', price: 3.05, taxRate: 20 },
  { id: 's1', name: 'Chefsalat', category: 'Salate', price: 9.5, taxRate: 10 },
];

describe('getProductsForGroup', () => {
  it('matches normalized catalog category names for legacy group keys', () => {
    const pizzas = CATEGORY_GROUPS.find((g) => g.name === 'pizzas')!;
    const drinks = CATEGORY_GROUPS.find((g) => g.name === 'drinks')!;

    expect(getProductsForGroup(pizzas, normalizedCatalogProducts).map((p) => p.id)).toEqual([
      'p1',
      'p2',
    ]);
    expect(getProductsForGroup(drinks, normalizedCatalogProducts).map((p) => p.id)).toEqual(['d1']);
  });

  it('still matches legacy category labels when catalog is not normalized', () => {
    const legacyProducts: CatalogProduct[] = [
      { id: 'p1', name: 'Margherita', category: 'Pizza-mittel', price: 9.8, taxRate: 10 },
    ];
    const pizzas = CATEGORY_GROUPS.find((g) => g.name === 'pizzas')!;

    expect(getGroupProductIds(pizzas, legacyProducts)).toEqual(['p1']);
  });
});

describe('getSelectedCategoryNames', () => {
  it('maps selected group keys to demo JSON subcategory names, not wizard group keys', () => {
    const resolved = getSelectedCategoryNames(new Set(['pizzas']), CATEGORY_GROUPS);

    expect(resolved).toEqual(
      expect.arrayContaining([
        'Pizza-mittel',
        'Pizza-Partner',
        'Familien-Pizza',
        'Mexikanische-Pizza-mittel',
        'Mexikanische-Pizza-Partner',
        'Calzone',
      ])
    );
    expect(resolved).not.toContain('pizzas');
    expect(resolved).not.toContain('Pizza, mittel');
  });

  it('maps normalized catalog labels back to demo JSON names for import', () => {
    expect(toLegacyImportCategoryName('Pizza, mittel')).toBe('Pizza-mittel');
    expect(toLegacyImportCategoryName('Alkoholfreie Getränke')).toBe('Alkoholfreie-Getrnke');
    expect(toLegacyImportCategoryName('Salate')).toBe('Salate');
  });
});

describe('buildImportRequest', () => {
  it('sends demo JSON subcategory names instead of wizard group keys', () => {
    const catalog: DemoImportCatalog = {
      categories: [
        { name: 'Pizza, mittel', productCount: 1, sortOrder: 1, vatRate: 10 },
        { name: 'Pizza, Partner', productCount: 1, sortOrder: 2, vatRate: 10 },
      ],
      products: normalizedCatalogProducts.slice(0, 2),
    };

    const request = buildImportRequest(catalog, new Set(['p1', 'p2']), false, {
      selectedGroupNames: new Set(['pizzas']),
      categoryGroups: CATEGORY_GROUPS,
    });

    expect(request.selectedCategories).toEqual(
      expect.arrayContaining(['Pizza-mittel', 'Pizza-Partner'])
    );
    expect(request.selectedCategories).not.toContain('pizzas');
    expect(request.selectedCategories).not.toContain('Pizza, mittel');
  });
});
