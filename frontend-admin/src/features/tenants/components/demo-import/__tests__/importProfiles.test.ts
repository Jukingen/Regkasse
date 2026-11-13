import { describe, expect, it } from 'vitest';

import { CATEGORY_GROUPS } from '@/features/tenants/components/demo-import/categoryGroups';
import {
  BUILTIN_DEMO_IMPORT_PROFILES,
  applyDemoImportProfile,
} from '@/features/tenants/components/demo-import/importProfiles';
import type { CatalogProduct } from '@/features/tenants/components/demo-import/utils';

const mockProducts: CatalogProduct[] = [
  { id: 'p1', name: 'Margherita', category: 'Pizza-mittel', price: 8.5, taxRate: 10 },
  { id: 'p2', name: 'Chefsalat', category: 'Salate', price: 6.9, taxRate: 10 },
  { id: 'p3', name: 'Cola', category: 'Alkoholfreie-Getrnke', price: 3.5, taxRate: 20 },
  { id: 'k1', name: 'Döner', category: 'Kebap', price: 7.5, taxRate: 10 },
];

describe('applyDemoImportProfile', () => {
  it('loads builtin restaurant standard groups with all matching products', () => {
    const profile = BUILTIN_DEMO_IMPORT_PROFILES.find((p) => p.id === 'restaurant-standard')!;
    const applied = applyDemoImportProfile(profile, CATEGORY_GROUPS, mockProducts);

    expect(applied.groupNames.has('pizzas')).toBe(true);
    expect(applied.groupNames.has('salads')).toBe(true);
    expect(applied.groupNames.has('drinks')).toBe(true);
    expect(applied.productIds.has('p1')).toBe(true);
    expect(applied.productIds.has('p2')).toBe(true);
    expect(applied.productIds.has('p3')).toBe(true);
    expect(applied.productIds.has('k1')).toBe(false);
  });

  it('restores exact product ids when saved on profile', () => {
    const applied = applyDemoImportProfile(
      {
        id: 'custom',
        name: 'Custom',
        groupNames: ['pizzas'],
        selectedProductIds: ['p3'],
        source: 'saved',
      },
      CATEGORY_GROUPS,
      mockProducts
    );

    expect(applied.productIds.size).toBe(1);
    expect(applied.productIds.has('p3')).toBe(true);
  });
});
