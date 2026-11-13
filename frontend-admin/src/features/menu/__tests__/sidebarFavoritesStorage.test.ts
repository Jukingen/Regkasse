import { beforeEach, describe, expect, it } from 'vitest';

import { sidebarFavoritesStorageKey } from '@/features/menu/menuConfig';
import {
  loadSidebarFavorites,
  saveSidebarFavorites,
  seedSidebarFavoritesIfMissing,
} from '@/features/menu/sidebarFavoritesStorage';
import { ADMIN_SIDEBAR_GROUP_KEYS } from '@/shared/adminSidebarNavigation';

describe('sidebarFavoritesStorage', () => {
  beforeEach(() => {
    const storage: Record<string, string> = {};
    vi.stubGlobal('localStorage', {
      getItem: (k: string) => storage[k] ?? null,
      setItem: (k: string, v: string) => {
        storage[k] = v;
      },
      removeItem: (k: string) => {
        delete storage[k];
      },
    });
  });

  it('returns null when the user has no saved row', () => {
    expect(loadSidebarFavorites('u1')).toBeNull();
  });

  it('round-trips an array payload under sidebar_favorites_{userId}', () => {
    saveSidebarFavorites('u1', ['/receipts', '/products']);
    expect(window.localStorage.getItem(sidebarFavoritesStorageKey('u1'))).toBe(
      JSON.stringify(['/receipts', '/products'])
    );
    expect(loadSidebarFavorites('u1')).toEqual({ favoriteIds: ['/receipts', '/products'] });
  });

  it('seeds Manager defaults only once', () => {
    const first = seedSidebarFavoritesIfMissing('mgr', 'Manager');
    expect(first).toContain(ADMIN_SIDEBAR_GROUP_KEYS.operations);
    expect(first).toContain('/receipts');
    saveSidebarFavorites('mgr', []);
    expect(seedSidebarFavoritesIfMissing('mgr', 'Manager')).toEqual([]);
  });
});
