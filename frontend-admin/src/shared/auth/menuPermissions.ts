/**
 * Menu item key → required permission(s). Used to filter sidebar by permission.
 * Sidebar leaf keys must match `SIDEBAR_NAV_ITEM_CATALOG` / RKSV model (`sidebarRouteCoverage` test).
 * Deep links / direct URL entry still require `routePermissions.ROUTE_PERMISSIONS` (can include paths
 * with no sidebar row, e.g. `/orders` — see `ROUTE_GUARD_PATHS_WITHOUT_SIDEBAR_LEAF`).
 * Empty = show when authenticated. One permission = require it; array = require any.
 */
import { ROUTE_PERMISSIONS } from './routePermissions';

// Deprecate direct usage; proxy to route permissions.
/** @deprecated Use ROUTE_PERMISSIONS directly */
export const MENU_PERMISSION: Record<string, string | string[] | undefined> = ROUTE_PERMISSIONS as Record<string, string | string[] | undefined>;

export function isMenuItemAllowed(
  key: string,
  permissions: string[] | undefined
): boolean {
  const required = ROUTE_PERMISSIONS[key];
  if (required === undefined) return true;
  if (!permissions?.length) return false;
  const arr = Array.isArray(required) ? required : [required];
  return arr.some((p) => permissions.includes(p));
}
