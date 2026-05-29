import { describe, expect, it } from 'vitest';

import {
  normalizeProductTextLocale,
  resolveProductDisplayDescription,
  resolveProductDisplayName,
} from '../utils/productLocalization';

describe('productLocalization', () => {
  it('resolves English product name with fallback', () => {
    const name = resolveProductDisplayName(
      {
        name: 'Pizza Margherita',
        nameDe: 'Pizza Margherita',
        nameEn: 'Margherita Pizza',
      },
      'en',
    );
    expect(name).toBe('Margherita Pizza');
  });

  it('resolves German description', () => {
    const desc = resolveProductDisplayDescription(
      {
        descriptionDe: 'mit Tomaten',
        descriptionEn: 'with tomatoes',
      },
      'de',
    );
    expect(desc).toBe('mit Tomaten');
  });

  it('normalizes locale codes', () => {
    expect(normalizeProductTextLocale('en-US')).toBe('en');
    expect(normalizeProductTextLocale('tr')).toBe('tr');
    expect(normalizeProductTextLocale(undefined)).toBe('de');
  });
});
