/**
 * Access & roles hub: canonical App Router paths for secondary nav.
 * Sidebar IA: nested `grp-access` under Verwaltung (`adminSidebarRegistry.ts`).
 */
export const ACCESS_AREA_ROUTE_PATHS = [
  '/admin/access',
  '/admin/users',
  '/admin/access/roles',
  '/admin/access/matrix',
  '/admin/access/permission-history',
  '/admin/access/permission-requests',
  '/admin/access/permission-packages',
  '/admin/access/permission-backups',
  '/admin/access/permission-stats',
] as const;

export type AccessAreaRoutePath = (typeof ACCESS_AREA_ROUTE_PATHS)[number];

export const ACCESS_HUB_LANDING_PATH = '/admin/access' as const;
