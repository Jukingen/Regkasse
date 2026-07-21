import { describe, expect, it } from 'vitest';

import { CATEGORY_GROUPS } from '@/features/tenants/components/demo-import/categoryGroups';
import {
  TAX_BULK_FIX_PRESETS,
  analyzeTaxSelection,
  collectProductIdsForBulkPreset,
} from '@/features/tenants/components/demo-import/taxValidation';
import type { CatalogProduct } from '@/features/tenants/components/demo-import/utils';

const catalogProducts: CatalogProduct[] = [
  { id: 'd1', name: 'Cola', category: 'Alkoholfreie-Getrnke', price: 3, taxRate: 20 },
  { id: 'p1', name: 'Pizza', category: 'Pizza-mittel', price: 9, taxRate: 10 },
  { id: 'p2', name: 'Pizza Spezial', category: 'Pizza-mittel', price: 11, taxRate: 20 },
];

const categoryVat = new Map<string, number>([
  ['Alkoholfreie-Getrnke', 20],
  ['Pizza-mittel', 10],
]);

describe('analyzeTaxSelection', () => {
  it('flags invalid tax rate', () => {
    const summary = analyzeTaxSelection([catalogProducts[0]], categoryVat, { d1: 7 });
    expect(summary.invalidProductIds.has('d1')).toBe(true);
    expect(summary.issues.some((i) => i.type === 'invalid_rate')).toBe(true);
  });

  it('warns when product tax differs from category default', () => {
    const summary = analyzeTaxSelection([catalogProducts[2]], categoryVat, {});
    expect(summary.mismatchProductIds.has('p2')).toBe(true);
    expect(summary.issues.some((i) => i.type === 'category_mismatch')).toBe(true);
  });

  it('warns on mixed rates within same category', () => {
    const summary = analyzeTaxSelection([catalogProducts[1], catalogProducts[2]], categoryVat, {});
    expect(summary.mixedCategoryNames.has('Pizza-mittel')).toBe(true);
    expect(summary.issues.some((i) => i.type === 'mixed_category_rates')).toBe(true);
  });
});

describe('TAX_BULK_FIX_PRESETS', () => {
  it('collects drink product ids for 20% preset', () => {
    const preset = TAX_BULK_FIX_PRESETS.find((p) => p.id === 'drinks-20')!;
    const ids = collectProductIdsForBulkPreset(
      preset,
      CATEGORY_GROUPS,
      catalogProducts,
      new Set(['d1', 'p1'])
    );
    expect(ids).toEqual(['d1']);
  });
});
