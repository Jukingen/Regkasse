import { beforeEach, describe, expect, it } from 'vitest';

import { useFavoritesStore } from '@/features/menu/favoritesStore';
import { sidebarFavoritesStorageKey } from '@/features/menu/menuConfig';
import { ADMIN_SIDEBAR_GROUP_KEYS } from '@/shared/adminSidebarNavigation';

describe('favoritesStore', () => {
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
    useFavoritesStore.getState().resetSession();
  });

  it('hydrates SuperAdmin defaults and persists them', () => {
    const visible = new Set([
      ADMIN_SIDEBAR_GROUP_KEYS.dashboard,
      '/admin/tenants',
      '/admin/licenses',
      '/admin/tse-management',
      '/backup',
    ]);
    useFavoritesStore.getState().hydrate('sa-1', 'SuperAdmin', visible);
    const ids = useFavoritesStore.getState().favoriteIds;
    expect(ids).toEqual([
      ADMIN_SIDEBAR_GROUP_KEYS.dashboard,
      '/admin/tenants',
      '/admin/licenses',
      '/admin/tse-management',
      '/backup',
    ]);
    expect(window.localStorage.getItem(sidebarFavoritesStorageKey('sa-1'))).toBeTruthy();
  });

  it('rejects an 8th favorite and prunes inaccessible keys', () => {
    const visible = new Set(['/a', '/b', '/c', '/d', '/e', '/f', '/g', '/h']);
    window.localStorage.setItem(
      sidebarFavoritesStorageKey('u2'),
      JSON.stringify(['/a', '/b', '/c', '/d', '/e', '/f', '/g'])
    );
    useFavoritesStore.getState().hydrate('u2', 'Cashier', visible);
    expect(useFavoritesStore.getState().addFavorite('/h')).toBe('max');

    useFavoritesStore.getState().hydrate('u2', 'Cashier', new Set(['/a', '/b']));
    expect(useFavoritesStore.getState().favoriteIds).toEqual(['/a', '/b']);
  });

  it('resetSession clears memory but keeps localStorage', () => {
    const visible = new Set(['/receipts']);
    window.localStorage.setItem(sidebarFavoritesStorageKey('u3'), JSON.stringify(['/receipts']));
    useFavoritesStore.getState().hydrate('u3', 'Manager', visible);
    useFavoritesStore.getState().resetSession();
    expect(useFavoritesStore.getState().hydrated).toBe(false);
    expect(loadFromStorage('u3')).toEqual(['/receipts']);
  });
});

function loadFromStorage(userId: string): unknown {
  return JSON.parse(window.localStorage.getItem(sidebarFavoritesStorageKey(userId)) ?? 'null');
}
