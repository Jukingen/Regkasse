import { describe, expect, it } from 'vitest';

import {
  TAX_TYPE_ENUM,
  formatProductUnitLabelForLocale,
  formatTaxTypeLabelForLocale,
  mapApiProductToUi,
  mapUiProductToApi,
  taxRateToType,
  taxTypeToRate,
} from '@/features/products/utils/productMapper';

const t = (key: string, options?: Record<string, string | number>) =>
  options ? `${key}:${JSON.stringify(options)}` : key;

describe('productMapper tax helpers', () => {
  it('maps tax type ↔ rate including defaults', () => {
    expect(taxTypeToRate(TAX_TYPE_ENUM.Standard)).toBe(20);
    expect(taxTypeToRate(TAX_TYPE_ENUM.Reduced)).toBe(10);
    expect(taxTypeToRate(TAX_TYPE_ENUM.Special)).toBe(13);
    expect(taxTypeToRate(TAX_TYPE_ENUM.ZeroRate)).toBe(0);
    expect(taxTypeToRate(TAX_TYPE_ENUM.ReducedNew)).toBe(4.9);
    expect(taxTypeToRate(99)).toBe(20);

    expect(taxRateToType(20)).toBe(TAX_TYPE_ENUM.Standard);
    expect(taxRateToType(10)).toBe(TAX_TYPE_ENUM.Reduced);
    expect(taxRateToType(13)).toBe(TAX_TYPE_ENUM.Special);
    expect(taxRateToType(0)).toBe(TAX_TYPE_ENUM.ZeroRate);
    expect(taxRateToType(4.9)).toBe(TAX_TYPE_ENUM.ReducedNew);
    expect(taxRateToType(7)).toBe(TAX_TYPE_ENUM.Standard);
  });

  it('formats tax labels with rate fallback', () => {
    expect(formatTaxTypeLabelForLocale(1, undefined, t)).toContain('products.taxLabels.standard');
    expect(formatTaxTypeLabelForLocale(2, 10, t)).toContain('products.taxLabels.reduced');
    expect(formatTaxTypeLabelForLocale(3, 13, t)).toContain('products.taxLabels.special');
    expect(formatTaxTypeLabelForLocale(4, 0, t)).toContain('products.taxLabels.zero');
    expect(formatTaxTypeLabelForLocale(5, 4.9, t)).toContain('products.taxLabels.reducedNew');
    expect(formatTaxTypeLabelForLocale(99, 20, t)).toContain('products.taxLabels.fallback');
  });

  it('localizes piece-unit synonyms and passes other units through', () => {
    expect(formatProductUnitLabelForLocale(null, t)).toBe('products.table.unitPieces');
    expect(formatProductUnitLabelForLocale('pcs', t)).toBe('products.table.unitPieces');
    expect(formatProductUnitLabelForLocale('Stück', t)).toBe('products.table.unitPieces');
    expect(formatProductUnitLabelForLocale('kg', t)).toBe('kg');
  });
});

describe('mapApiProductToUi', () => {
  it('returns empty product for nullish input', () => {
    expect(mapApiProductToUi(null)).toEqual({});
    expect(mapApiProductToUi(undefined)).toEqual({});
  });

  it('maps PascalCase API payload with nested tax group', () => {
    const ui = mapApiProductToUi({
      Id: 'p1',
      Name: 'Kaffee',
      NameDe: 'Kaffee',
      NameEn: 'Coffee',
      Price: 3.5,
      TaxType: 2,
      TaxGroup: {
        Id: 'tg1',
        Name: 'Reduced',
        Rate: 10,
        Color: '#f00',
        Icon: 'tag',
        AustrianCode: 'A',
      },
      IsActive: false,
      StockQuantity: 5,
      MinStockLevel: 1,
      Unit: 'pcs',
      Category: 'Drinks',
      CategoryId: 'c1',
      Barcode: '123',
      Cost: 1,
      CreatedAt: '2026-01-01T00:00:00Z',
    });

    expect(ui.id).toBe('p1');
    expect(ui.name).toBe('Kaffee');
    expect(ui.taxType).toBe(2);
    expect(ui.taxRate).toBe(10);
    expect(ui.taxGroupId).toBe('tg1');
    expect(ui.taxGroup).toMatchObject({
      id: 'tg1',
      name: 'Reduced',
      rate: 10,
      color: '#f00',
      austrianCode: 'A',
    });
    expect(ui.isActive).toBe(false);
  });

  it('prefers camelCase fields and derives taxRate from taxType', () => {
    const ui = mapApiProductToUi({
      id: 'p2',
      name: 'Wasser',
      taxType: 4,
      taxGroupId: 'tg-zero',
    });
    expect(ui.taxRate).toBe(0);
    expect(ui.taxGroupId).toBe('tg-zero');
    expect(ui.taxGroup).toBeNull();
    expect(ui.isActive).toBe(true);
  });
});

describe('mapUiProductToApi extras', () => {
  it('normalizes blank imageUrl to null and defaults unit/taxRate', () => {
    const payload = mapUiProductToApi({
      id: 'p1',
      name: 'Item',
      nameDe: 'Item',
      price: 1,
      categoryId: 'c1',
      category: '  Food  ',
      imageUrl: '   ',
      taxType: 3,
    } as never);

    expect(payload.imageUrl).toBeNull();
    expect(payload.category).toBe('Food');
    expect(payload.unit).toBe('pcs');
    expect(payload.taxType).toBe(3);
    expect(payload.taxRate).toBe(13);
    expect(payload.isFiscalCompliant).toBe(true);
  });

  it('builds canonical name from localized fields', () => {
    const payload = mapUiProductToApi({
      id: 'p1',
      name: '',
      nameEn: 'Soup',
      price: 4,
      categoryId: 'c1',
      category: 'Soup',
      taxRate: 20,
      taxType: 1,
      taxGroupId: 'tg1',
    } as never);

    expect(payload.name).toBe('Soup');
    expect(payload.nameEn).toBe('Soup');
    expect(payload.taxGroupId).toBe('tg1');
  });
});
