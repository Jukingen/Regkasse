import { describe, expect, it } from 'vitest';

import {
  MANAGER_DEFAULT_FAVORITE_MENU_KEYS,
  MAX_SIDEBAR_FAVORITES,
  SUPER_ADMIN_DEFAULT_FAVORITE_MENU_KEYS,
  getDefaultFavoriteMenuKeys,
  sidebarFavoritesStorageKey,
} from '@/features/menu/menuConfig';
import { ADMIN_SIDEBAR_GROUP_KEYS } from '@/shared/adminSidebarNavigation';

describe('menuConfig', () => {
  it('caps favorites at 7', () => {
    expect(MAX_SIDEBAR_FAVORITES).toBe(7);
  });

  it('uses sidebar_favorites_{userId} storage keys', () => {
    expect(sidebarFavoritesStorageKey('user-1')).toBe('sidebar_favorites_user-1');
  });

  it('returns Manager defaults: Verkauf, Belege, Tagesabschluss, Produkte, RKSV', () => {
    expect(getDefaultFavoriteMenuKeys('Manager')).toEqual([
      ADMIN_SIDEBAR_GROUP_KEYS.operations,
      '/receipts',
      '/tagesabschluss',
      '/products',
      ADMIN_SIDEBAR_GROUP_KEYS.rksv,
    ]);
    expect(MANAGER_DEFAULT_FAVORITE_MENU_KEYS).toHaveLength(5);
  });

  it('returns SuperAdmin defaults: Dashboard, Mandanten, Lizenzen, TSE-Verwaltung, Backup', () => {
    expect(getDefaultFavoriteMenuKeys('SuperAdmin')).toEqual([
      ADMIN_SIDEBAR_GROUP_KEYS.dashboard,
      '/admin/tenants',
      '/admin/licenses',
      '/admin/tse-management',
      '/backup',
    ]);
    expect(SUPER_ADMIN_DEFAULT_FAVORITE_MENU_KEYS).toHaveLength(5);
  });

  it('returns no defaults for other roles', () => {
    expect(getDefaultFavoriteMenuKeys('Cashier')).toEqual([]);
    expect(getDefaultFavoriteMenuKeys(null)).toEqual([]);
  });
});
