/**
 * Route coverage helpers for permission drift tests.
 * Non-RKSV leaves are derived from `adminSidebarRegistry` (single source of truth).
 */

import { getSidebarCatalogLeafMenuKeys } from '@/shared/adminSidebarRegistry';

/** Every non-RKSV sidebar leaf path (matches `SIDEBAR_NAV_ITEM_CATALOG` menuKeys). */
export const ADMIN_SIDEBAR_NON_RKSV_LEAF_ROUTE_KEYS = getSidebarCatalogLeafMenuKeys() as readonly string[];

/**
 * Protected routes that are not exposed in the sidebar (intentional).
 * `/orders` is registered in `ROUTE_PERMISSIONS` for direct/deep links; there is no `MENU_PERMISSION`
 * row because the sidebar does not surface this area yet.
 */
export const ROUTE_GUARD_PATHS_WITHOUT_SIDEBAR_LEAF = ['/orders'] as const;
