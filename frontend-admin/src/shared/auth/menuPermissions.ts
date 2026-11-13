/**
 * Menu item key → required permission(s). Used to filter sidebar by permission.
 * Sidebar leaf keys must match `SIDEBAR_NAV_ITEM_CATALOG` / RKSV model (`sidebarRouteCoverage` test).
 * Deep links / direct URL entry still require `routePermissions.ROUTE_PERMISSIONS` (can include paths
 * with no sidebar row, e.g. `/orders` — see `ROUTE_GUARD_PATHS_WITHOUT_SIDEBAR_LEAF`).
 * Empty = show when authenticated. One permission = require it; array = require any.
 *
 * Logical IA areas (Betrieb, RKSV, …): see `menuPermissionRegistry.ts` (`MENU_PERMISSIONS`).
 */
import { canShowRksvMenu } from '@/features/auth/constants/roles';

import { permissionImplied } from './permissionImplication';
import { ROUTE_PERMISSIONS } from './routePermissions';

export {
  MENU_AREA_PRIMARY_PATH,
  MENU_PERMISSIONS as MENU_AREA_PERMISSIONS,
  canAccessMenuArea,
  getMenuPermissionEntry,
  getMenuPermissionState,
  resolveMenuAreaKey,
  resolveMenuAreaPermissions,
  tryRegistryMenuVisibility,
  type MenuAreaKey,
  type MenuPermissionEntry,
  type MenuPermissionState,
} from './menuPermissionRegistry';

// Deprecate direct usage; proxy to route permissions.
/** @deprecated Use ROUTE_PERMISSIONS directly */
export const MENU_PERMISSION: Record<string, string | string[] | undefined> =
  ROUTE_PERMISSIONS as Record<string, string | string[] | undefined>;

/**
 * Sidebar leaf visibility — implication-aware (manage → view, digital.manage → digital.view, …).
 * Matches `hasPermission` / `permissionImplied` used by `useMenuPermissions`.
 */
export function isMenuItemAllowed(key: string, permissions: string[] | undefined): boolean {
  const required = ROUTE_PERMISSIONS[key];
  if (required === undefined) return true;
  if (!permissions?.length) return false;
  const arr = Array.isArray(required) ? required : [required];
  /** Empty array = any authenticated user with permission claims (e.g. `/dashboard`). */
  if (arr.length === 0) return permissions.length > 0;
  return arr.some((p) => permissionImplied(p, permissions));
}

/** RKSV hub visibility — permission-first; legacy role fallback when JWT has no permission claims. */
export function isRksvMenuAreaAllowed(
  permissions: string[] | undefined,
  role?: string | null
): boolean {
  if (permissions && permissions.length > 0) {
    return (
      isMenuItemAllowed('/rksv', permissions) || isMenuItemAllowed('/rksv/operations', permissions)
    );
  }
  return canShowRksvMenu(role);
}

/** RKSV sidebar / command-palette leaf — per-route permission when claims exist, else role fallback. */
export function isRksvRouteKeyAllowed(
  menuKey: string,
  permissions: string[] | undefined,
  role?: string | null
): boolean {
  if (!menuKey.startsWith('/rksv')) return true;
  if (permissions && permissions.length > 0) {
    return isMenuItemAllowed(menuKey, permissions);
  }
  return canShowRksvMenu(role);
}
