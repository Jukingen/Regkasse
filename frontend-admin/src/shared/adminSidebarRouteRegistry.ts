/**
 * Route coverage helpers for permission drift tests.
 *
 * Non-RKSV sidebar leaves: derived from `adminSidebarRegistry` (`getSidebarCatalogLeafMenuKeys`).
 * RKSV leaves: from `buildRksvMenuGroups` in coverage tests (`sidebarRouteCoverage.test.ts`).
 * Intentionally unlisted sidebar routes (guarded only): `ROUTE_GUARD_PATHS_WITHOUT_SIDEBAR_LEAF`.
 */
import { getSidebarCatalogLeafMenuKeys } from '@/shared/adminSidebarRegistry';

/** Every non-RKSV sidebar leaf path (matches `SIDEBAR_NAV_ITEM_CATALOG` menuKeys). */
export const ADMIN_SIDEBAR_NON_RKSV_LEAF_ROUTE_KEYS =
  getSidebarCatalogLeafMenuKeys() as readonly string[];

/**
 * Protected routes that are not exposed in the sidebar (intentional).
 * `/orders` is registered in `ROUTE_PERMISSIONS` for direct/deep links; there is no `MENU_PERMISSION`
 * row because the sidebar does not surface this area yet.
 */
export const ROUTE_GUARD_PATHS_WITHOUT_SIDEBAR_LEAF = [
  '/orders',
  '/vouchers/new',
  /** Legacy alias → `/receipts`. */
  '/sales',
  /** Legacy alias → `/payments/storno-refund-audit`. */
  '/storno',
  /** New sale wizard — catalog entry is `sidebarHidden`; reached from overview/sales/tenant hub. */
  '/admin/billing/sales/new',
  /** Sale detail `/admin/billing/sales/{id}` — prefix-guarded via `/admin/billing/sales` (no sidebar leaf). */
] as const;
