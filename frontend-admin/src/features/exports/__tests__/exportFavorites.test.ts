import { describe, expect, it } from 'vitest';

import {
  DEFAULT_EXPORT_FAVORITE_IDS,
  isExportTypeId,
} from '@/features/exports/exportTypeCatalog';
import {
  reorderExportFavoriteIds,
  toggleExportFavoriteId,
} from '@/features/exports/exportFavoritesStorage';
import { moveExportFavorite } from '@/features/exports/reorderExportFavorites';

describe('exportFavorites', () => {
  it('isExportTypeId validates catalog ids', () => {
    expect(isExportTypeId('dep-export')).toBe(true);
    expect(isExportTypeId('nope')).toBe(false);
  });

  it('toggleExportFavoriteId adds and removes', () => {
    const base = [...DEFAULT_EXPORT_FAVORITE_IDS];
    const without = toggleExportFavoriteId(base, 'dep-export');
    expect(without).not.toContain('dep-export');
    const withInvoice = toggleExportFavoriteId(without, 'invoice');
    expect(withInvoice).toContain('invoice');
  });

  it('moveExportFavorite reorders', () => {
    const ids = ['dep-export', 'backup', 'tagesbericht'] as const;
    const next = moveExportFavorite([...ids], 'dep-export', 'tagesbericht');
    expect(next).toEqual(['backup', 'tagesbericht', 'dep-export']);
    expect(reorderExportFavoriteIds([...ids], 'a', 'b')).toBeNull();
  });
});
