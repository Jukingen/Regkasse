/**
 * Role-based default sidebar favorites (menu keys).
 * Keys are Ant Design Menu item keys: catalog `menuKey` paths or `grp-*` group ids.
 */

import { isManager, isSuperAdmin } from '@/features/auth/constants/roles';
import { ADMIN_SIDEBAR_GROUP_KEYS } from '@/shared/adminSidebarNavigation';

export const MAX_SIDEBAR_FAVORITES = 7;

/** localStorage key: `sidebar_favorites_{userId}` */
export const SIDEBAR_FAVORITES_STORAGE_PREFIX = 'sidebar_favorites_';

export function sidebarFavoritesStorageKey(userId: string): string {
  return `${SIDEBAR_FAVORITES_STORAGE_PREFIX}${userId || 'anon'}`;
}

/** Mandanten-Admin: Verkauf, Belege, Tagesabschluss, Produkte, RKSV & TSE */
export const MANAGER_DEFAULT_FAVORITE_MENU_KEYS = [
  ADMIN_SIDEBAR_GROUP_KEYS.operations,
  '/receipts',
  '/tagesabschluss',
  '/products',
  ADMIN_SIDEBAR_GROUP_KEYS.rksv,
] as const;

/** Super Admin: Dashboard, Mandanten, Lizenzen, TSE-Verwaltung, Backup */
export const SUPER_ADMIN_DEFAULT_FAVORITE_MENU_KEYS = [
  ADMIN_SIDEBAR_GROUP_KEYS.dashboard,
  '/admin/tenants',
  '/admin/licenses',
  '/admin/tse-management',
  '/backup',
] as const;

export function getDefaultFavoriteMenuKeys(role: string | null | undefined): readonly string[] {
  if (isSuperAdmin(role)) {
    return SUPER_ADMIN_DEFAULT_FAVORITE_MENU_KEYS;
  }
  if (isManager(role)) {
    return MANAGER_DEFAULT_FAVORITE_MENU_KEYS;
  }
  return [];
}
