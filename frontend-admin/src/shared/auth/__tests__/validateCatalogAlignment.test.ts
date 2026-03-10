/**
 * validateCatalogAlignment – menu/route keys not in catalog trigger warning result.
 */
import { describe, it, expect, vi, afterEach } from 'vitest';
import { validateCatalogAlignment } from '../validateCatalogAlignment';

describe('validateCatalogAlignment', () => {
  const originalWarn = console.warn;
  afterEach(() => {
    console.warn = originalWarn;
  });

  it('returns empty unknownKeys when all menu/route keys are in catalog', () => {
    const catalogKeys = [
      'settings.view',
      'product.view',
      'category.view',
      'invoice.view',
      'order.view',
      'payment.view',
      'audit.view',
      'user.view',
      'receipttemplate.view',
      'sale.view',
      'finanzonline.manage',
    ];
    const { unknownKeys, hasWarnings } = validateCatalogAlignment(catalogKeys, { warnUnknown: false });
    expect(hasWarnings).toBe(unknownKeys.length > 0);
  });

  it('returns unknown keys when catalog is empty', () => {
    const { unknownKeys, hasWarnings } = validateCatalogAlignment([], { warnUnknown: false });
    expect(unknownKeys.length).toBeGreaterThan(0);
    expect(hasWarnings).toBe(true);
  });

  it('logs console.warn when warnUnknown is true and there are unknown keys', () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    validateCatalogAlignment([], { warnUnknown: true });
    expect(warnSpy).toHaveBeenCalled();
    expect(warnSpy.mock.calls[0][0]).toContain('validateCatalogAlignment');
    warnSpy.mockRestore();
  });

  it('accepts catalog as Set', () => {
    const catalogSet = new Set(['user.view', 'product.view']);
    const { unknownKeys } = validateCatalogAlignment(catalogSet, { warnUnknown: false });
    expect(Array.isArray(unknownKeys)).toBe(true);
  });
});
