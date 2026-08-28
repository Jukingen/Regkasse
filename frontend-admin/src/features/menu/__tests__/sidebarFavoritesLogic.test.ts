import { describe, expect, it } from 'vitest';

import { MAX_SIDEBAR_FAVORITES } from '@/features/menu/menuConfig';
import {
  addFavoriteMenuKey,
  collectAllMenuKeys,
  pruneInaccessibleFavorites,
  removeFavoriteMenuKey,
  reorderFavoriteMenuKeys,
  sanitizeFavoriteMenuKeys,
} from '@/features/menu/sidebarFavoritesLogic';

describe('sidebarFavoritesLogic', () => {
  it('sanitizes keys, drops duplicates and unknown values, and caps at max', () => {
    expect(
      sanitizeFavoriteMenuKeys([
        '/receipts',
        'not-a-key',
        '/receipts',
        'grp-operations',
        42,
        '/products',
      ])
    ).toEqual(['/receipts', 'grp-operations', '/products']);

    const overflow = Array.from({ length: 12 }, (_, i) => `/item-${i}`);
    expect(sanitizeFavoriteMenuKeys(overflow)).toHaveLength(MAX_SIDEBAR_FAVORITES);
  });

  it('adds until max then reports max', () => {
    const six = ['/a', '/b', '/c', '/d', '/e', '/f'];
    const added = addFavoriteMenuKey(six, '/g');
    expect(added.result).toBe('added');
    expect(added.ids).toHaveLength(7);

    const atMax = addFavoriteMenuKey(added.ids, '/h');
    expect(atMax.result).toBe('max');
    expect(atMax.ids).toEqual(added.ids);

    const exists = addFavoriteMenuKey(added.ids, '/a');
    expect(exists.result).toBe('exists');
  });

  it('removes and reorders', () => {
    expect(removeFavoriteMenuKey(['/a', '/b', '/c'], '/b')).toEqual(['/a', '/c']);
    expect(reorderFavoriteMenuKeys(['/a', '/b', '/c'], '/a', '/c')).toEqual(['/b', '/c', '/a']);
    expect(reorderFavoriteMenuKeys(['/a', '/b'], '/a', '/missing')).toBeNull();
  });

  it('prunes keys missing from the visible menu tree, but not when the tree is empty', () => {
    const ids = ['/receipts', '/products', 'grp-rksv'];
    expect(pruneInaccessibleFavorites(ids, new Set())).toEqual(ids);
    expect(pruneInaccessibleFavorites(ids, new Set(['/receipts', 'grp-rksv']))).toEqual([
      '/receipts',
      'grp-rksv',
    ]);
  });

  it('collects leaf and group keys from a menu tree', () => {
    const keys = collectAllMenuKeys([
      { type: 'divider', key: 'd1' },
      {
        key: 'grp-operations',
        children: [{ key: '/receipts' }, { key: '/tagesabschluss' }],
      },
    ]);
    expect(keys).toEqual(['grp-operations', '/receipts', '/tagesabschluss']);
  });
});
